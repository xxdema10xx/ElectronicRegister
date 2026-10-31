using System.Data.Common;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class IdentityAccountRepository(ElectronicRegisterContext db) : IIdentityAccountRepository
{
    public async Task<Guid> CreateAsync(string email, string passwordHash, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Historical required column, never an authorization decision or profile assignment.
            db.Users.Add(new() { Id = id, Email = email, PasswordHash = passwordHash, Role = "student" });
            await db.SaveChangesAsync(ct);
            await AuditAsync(id, "account.registered", ct);
            await tx.CommitAsync(ct);
            return id;
        }
        catch (DbUpdateException ex)when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            throw new ElectronicRegisterAPI.Domain.Exceptions.BusinessRuleException("Account non disponibile.");
        }
    }

    private async Task<DbCommand> CommandAsync(
        string sql,
        Guid userId,
        CancellationToken ct,
        params (string Name, object Value)[] parameters
    )
    {
        await db.Database.OpenConnectionAsync(ct);
        var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        foreach (var(name, value)in parameters.Prepend(("@user", (object)userId)))
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    public async Task<IdentityAccount?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        // One statement: credentials and generation must come from the same snapshot.
        await using var command = await CommandAsync("SELECT u.id,u.email,u.password_hash,s.display_name,COALESCE(s.generation,0) FROM `user` u LEFT JOIN identity_account_state s ON s.user_id=u.id WHERE u.id=@user", userId, ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt64(4)) : null;
    }

    public async Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var command = await CommandAsync("SELECT u.id,u.email,u.password_hash,s.display_name,COALESCE(s.generation,0) FROM `user` u LEFT JOIN identity_account_state s ON s.user_id=u.id WHERE u.email=@email", Guid.Empty, ct, ("@email", email));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt64(4)) : null;
    }

    public async Task<long> GenerationAsync(Guid userId, CancellationToken ct = default)
    {
        await using var command = await CommandAsync("SELECT COALESCE(s.generation,0) FROM `user` u LEFT JOIN identity_account_state s ON s.user_id=u.id WHERE u.id=@user", userId, ct);
        var value = await command.ExecuteScalarAsync(ct);
        return value is null ? -1 : Convert.ToInt64(value);
    }

    public async Task RequireGenerationAsync(Guid userId, long generation, CancellationToken ct = default)
    {
        // Recipient acceptance calls this after its IAM mutex and before commit.
        // Holding the global identity row prevents password rotation racing acceptance.
        var locking = db.Database.CurrentTransaction is null ? "" : " FOR UPDATE";
        await using var command = await CommandAsync("SELECT COALESCE(s.generation,0) FROM `user` u LEFT JOIN identity_account_state s ON s.user_id=u.id WHERE u.id=@user" + locking, userId, ct);
        var value = await command.ExecuteScalarAsync(ct);
        if (value is null || generation < 0 || Convert.ToInt64(value) != generation)
            throw new UnauthorizedAccessException("Sessione revocata.");
    }

    private async Task LockAsync(Guid userId, long generation, CancellationToken ct)
    {
        // Global identity operation: no tenant authority is inferred or consulted.
        await using var user = await CommandAsync("SELECT id FROM `user` WHERE id=@user FOR UPDATE", userId, ct);
        if (await user.ExecuteScalarAsync(ct)is null)
            throw new UnauthorizedAccessException();
        await using var insert = await CommandAsync("INSERT INTO identity_account_state(user_id,generation) VALUES(@user,0) ON DUPLICATE KEY UPDATE user_id=user_id", userId, ct);
        await insert.ExecuteNonQueryAsync(ct);
        await using var state = await CommandAsync("SELECT generation FROM identity_account_state WHERE user_id=@user FOR UPDATE", userId, ct);
        if (Convert.ToInt64(await state.ExecuteScalarAsync(ct)) != generation || generation < 0 || generation == long.MaxValue)
            throw new UnauthorizedAccessException("Sessione revocata.");
    }

    private async Task AuditAsync(Guid userId, string action, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO identity_account_event(id,user_id,action) VALUES({Guid.NewGuid()},{userId},{action})", ct);
    }

    public async Task<Guid?> BeginPasswordAttemptAsync(Guid userId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO identity_password_attempt(user_id,window_start,attempts) VALUES({userId},UTC_TIMESTAMP(6),0) ON DUPLICATE KEY UPDATE user_id=user_id", ct);
        await using var select = await CommandAsync("SELECT attempts,TIMESTAMPDIFF(SECOND,window_start,UTC_TIMESTAMP(6)),lease_until>UTC_TIMESTAMP(6) FROM identity_password_attempt WHERE user_id=@user FOR UPDATE", userId, ct);
        int attempts;
        bool expired, busy;
        await using (var reader = await select.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Password quota unavailable.");
            attempts = reader.GetInt32(0);
            expired = reader.GetInt64(1) >= 60;
            busy = !reader.IsDBNull(2) && reader.GetBoolean(2);
        }
        if (busy || !expired && attempts >= 5)
        {
            await tx.CommitAsync(ct);
            return null;
        }
        var lease = Guid.NewGuid();
        await using var update = await CommandAsync("UPDATE identity_password_attempt SET attempts=IF(@expired,1,attempts+1),window_start=IF(@expired,UTC_TIMESTAMP(6),window_start),lease_id=@lease,lease_until=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 30 SECOND) WHERE user_id=@user", userId, ct, ("@expired", expired), ("@lease", lease));
        await update.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return lease;
    }

    public async Task EndPasswordAttemptAsync(Guid userId, Guid lease, CancellationToken ct = default)
    {
        await using var command = await CommandAsync("UPDATE identity_password_attempt SET lease_id=NULL,lease_until=NULL WHERE user_id=@user AND lease_id=@lease", userId, ct, ("@lease", lease));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        long generation,
        string expectedHash,
        string newHash,
        CancellationToken ct = default
    )
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(userId, generation, ct);
        await using var update = await CommandAsync("UPDATE `user` SET password_hash=@new WHERE id=@user AND BINARY password_hash=BINARY @old", userId, ct, ("@new", newHash), ("@old", expectedHash));
        if (await update.ExecuteNonQueryAsync(ct) != 1)
            throw new UnauthorizedAccessException("Credenziali modificate.");
        await using var revoke = await CommandAsync("UPDATE identity_account_state SET generation=generation+1 WHERE user_id=@user", userId, ct);
        await revoke.ExecuteNonQueryAsync(ct);
        await AuditAsync(userId, "password.changed", ct);
        await tx.CommitAsync(ct);
    }

    public async Task UpdateDisplayNameAsync(
        Guid userId,
        long generation,
        string displayName,
        CancellationToken ct = default
    )
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(userId, generation, ct);
        await using var update = await CommandAsync("UPDATE identity_account_state SET display_name=@name WHERE user_id=@user", userId, ct, ("@name", displayName));
        await update.ExecuteNonQueryAsync(ct);
        await AuditAsync(userId, "profile.updated", ct);
        await tx.CommitAsync(ct);
    }
}
