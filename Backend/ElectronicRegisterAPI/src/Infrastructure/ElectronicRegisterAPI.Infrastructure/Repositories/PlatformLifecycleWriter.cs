using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

// Lifecycle transitions inside the caller's transaction, with hand-written parameterized SQL.
// Every transition is guarded by the current state (UPDATE ... WHERE lifecycle_state=...),
// advances the identity generation exactly once (revoking every earlier token) and appends
// one ledger event whose id is derived from user, transition and reached generation.
internal sealed class PlatformLifecycleWriter(ElectronicRegisterContext db, PlatformIdentitySchema schema, IMailboxProvisioner mailbox)
{
    public Task<bool> InstalledAsync(CancellationToken ct) => schema.InstalledAsync(db, ct);

    public static Guid EventId(Guid userId, string transition, long generation)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("er-lifecycle-v1|" + userId.ToString("D") + "|" + transition + "|" + generation))[..16];
        bytes[7] = (byte)(bytes[7] & 0x0F | 0x80);
        bytes[8] = (byte)(bytes[8] & 0x3F | 0x80);
        return new Guid(bytes);
    }

    private DbCommand Command(string sql, params (string Name, object? Value)[] values)
    {
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Transazione richiesta.");
        var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private async Task<int> ExecuteAsync(string sql, CancellationToken ct, params (string Name, object? Value)[] values)
    {
        await using var command = Command(sql, values);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<long> AdvanceGenerationAsync(Guid userId, CancellationToken ct)
    {
        await ExecuteAsync("INSERT INTO identity_account_state(user_id,generation) VALUES(@u,0) ON DUPLICATE KEY UPDATE user_id=user_id", ct, ("@u", userId));
        await using var read = Command("SELECT generation FROM identity_account_state WHERE user_id=@u FOR UPDATE", ("@u", userId));
        var generation = Convert.ToInt64(await read.ExecuteScalarAsync(ct));
        if (generation is < 0 or long.MaxValue)
            throw new InvalidOperationException("Generation esaurita.");
        if (await ExecuteAsync("UPDATE identity_account_state SET generation=generation+1 WHERE user_id=@u AND generation=@g", ct, ("@u", userId), ("@g", generation)) != 1)
            throw new InvalidOperationException("Avanzamento della generation non valido.");
        return generation + 1;
    }

    private async Task AppendAsync(
        Guid userId,
        string transition,
        string rule,
        long generation,
        CancellationToken ct
    ) =>
        await ExecuteAsync("INSERT INTO identity_lifecycle_event(id,user_id,transition,rule_code,rule_version,security_generation,occurred_at_utc) VALUES(@id,@u,@t,@r,@v,@g,UTC_TIMESTAMP(6))", ct,
            ("@id", EventId(userId, transition, generation)), ("@u", userId), ("@t", transition), ("@r", rule), ("@v", LifecycleRuleCodes.Version), ("@g", generation));

    public async Task<bool> CloseAsync(Guid userId, string rule, CancellationToken ct)
    {
        if (await ExecuteAsync("UPDATE `user` SET lifecycle_state='Closed',closed_at_utc=UTC_TIMESTAMP(6) WHERE id=@u AND lifecycle_state='Active'", ct, ("@u", userId)) != 1)
            return false;
        await AppendAsync(userId, LifecycleRuleCodes.Closed, rule, await AdvanceGenerationAsync(userId, ct), ct);
        return true;
    }

    // A new password hash is supplied only by re-creation with the same fiscal code; a
    // reactivated membership keeps the existing credential.
    public async Task<bool> ReactivateAsync(
        Guid userId,
        string rule,
        string? newPasswordHash,
        CancellationToken ct
    )
    {
        var sql = newPasswordHash is null
            ? "UPDATE `user` SET lifecycle_state='Active',reactivated_at_utc=UTC_TIMESTAMP(6) WHERE id=@u AND lifecycle_state='Closed'"
            : "UPDATE `user` SET lifecycle_state='Active',reactivated_at_utc=UTC_TIMESTAMP(6),password_hash=@h WHERE id=@u AND lifecycle_state='Closed'";
        if (await ExecuteAsync(sql, ct, ("@u", userId), ("@h", newPasswordHash)) != 1)
            return false;
        await AppendAsync(userId, LifecycleRuleCodes.Reactivated, rule, await AdvanceGenerationAsync(userId, ct), ct);
        if (newPasswordHash is not null)
            await ExecuteAsync("INSERT INTO identity_account_event(id,user_id,action) VALUES(@e,@u,@a)", ct, ("@e", Guid.NewGuid()), ("@u", userId), ("@a", IdentityAccountEvents.TemporaryPassword));
        return true;
    }

    // D-C: a membership that became active again reopens its Closed account in the same
    // transaction as the membership mutation. No effect without the successor schema.
    public async Task<bool> ReactivateMemberAsync(Guid institutionId, Guid membershipId, CancellationToken ct)
    {
        if (!await InstalledAsync(ct))
            return false;
        Guid user;
        string email;
        await using (var read = Command("SELECT u.id,u.email FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN `user` u ON u.id=m.user_id WHERE m.institution_id=@i AND m.id=@m AND m.status='active' AND i.status='active' AND u.lifecycle_state='Closed' FOR UPDATE",
            ("@i", institutionId), ("@m", membershipId)))
        await using (var reader = await read.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                return false;
            user = reader.GetGuid(0);
            email = reader.GetString(1);
        }
        if (!await ReactivateAsync(user, LifecycleRuleCodes.MembershipReactivated, null, ct))
            return false;
        await mailbox.CreateAsync(user, email, ct);
        return true;
    }

    // D-C: an institution that became active again reopens every Closed account that now
    // has an active membership there.
    public async Task<int> ReactivateInstitutionAsync(Guid institutionId, CancellationToken ct)
    {
        if (!await InstalledAsync(ct))
            return 0;
        var users = new List<(Guid Id, string Email)>();
        await using (var read = Command("SELECT u.id,u.email FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN `user` u ON u.id=m.user_id WHERE m.institution_id=@i AND m.status='active' AND i.status='active' AND u.lifecycle_state='Closed' ORDER BY u.id FOR UPDATE", ("@i", institutionId)))
        await using (var reader = await read.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                users.Add((reader.GetGuid(0), reader.GetString(1)));
        var count = 0;
        foreach (var (user, email) in users)
            if (await ReactivateAsync(user, LifecycleRuleCodes.InstitutionReactivated, null, ct))
            {
                await mailbox.CreateAsync(user, email, ct);
                count++;
            }
        return count;
    }
}
