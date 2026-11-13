using System.Data;
using System.Data.Common;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

// Global identity maintenance for the lifecycle worker. Candidate queries only pre-filter;
// the decision is always re-evaluated under the identity row lock in its own READ COMMITTED
// transaction. Lock order: identity row only (no tenant row, no email mutex), so it cannot
// deadlock with tenant-first writers; a concurrent membership reactivation waits for this
// row and then reopens the account in its own transaction (D-C).
internal sealed class PlatformLifecycleRepository(
    ElectronicRegisterContext db,
    PlatformLifecycleWriter writer,
    ILifecycleEvaluator evaluator,
    IMailboxProvisioner mailbox,
    IOnboardingFaultObserver faults
) : IPlatformLifecycleRepository
{
    private const string ActiveMembership = "SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id WHERE m.user_id=u.id AND m.status='active' AND i.status='active'";

    public Task<bool> InstalledAsync(CancellationToken ct) => writer.InstalledAsync(ct);

    private async Task<DbCommand> CommandAsync(string sql, CancellationToken ct, params (string Name, object? Value)[] values)
    {
        await db.Database.OpenConnectionAsync(ct);
        var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
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

    private async Task<IReadOnlyList<Guid>> IdsAsync(string sql, CancellationToken ct, params (string, object?)[] values)
    {
        await using var command = await CommandAsync(sql, ct, values);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var ids = new List<Guid>();
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    public Task<IReadOnlyList<Guid>> ClosureCandidatesAsync(
        Guid? after,
        int limit,
        DateTime studentStartedBeforeUtc,
        CancellationToken ct
    ) => IdsAsync(
        "SELECT u.id FROM `user` u WHERE u.lifecycle_state='Active' AND (@after IS NULL OR u.id>@after) AND NOT EXISTS(" + ActiveMembership + ")" +
        " AND NOT EXISTS(SELECT 1 FROM institution_invitation v WHERE v.recipient_user_id=u.id AND v.status='pending' AND v.expires_at_utc>UTC_TIMESTAMP(6))" +
        " AND COALESCE((SELECT MIN(m.joined_at) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id AND r.role='Student' WHERE m.user_id=u.id),'1000-01-01')<=@cutoff" +
        " ORDER BY u.id LIMIT @limit", ct, ("@after", after), ("@cutoff", studentStartedBeforeUtc), ("@limit", limit));

    public Task<IReadOnlyList<Guid>> ReactivationCandidatesAsync(Guid? after, int limit, CancellationToken ct) => IdsAsync(
        "SELECT u.id FROM `user` u WHERE u.lifecycle_state='Closed' AND (@after IS NULL OR u.id>@after) AND EXISTS(" + ActiveMembership + ") ORDER BY u.id LIMIT @limit",
        ct, ("@after", after), ("@limit", limit));

    public async Task<LifecycleAction> ApplyAsync(Guid userId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Ogni account richiede una propria unità di lavoro.");
        await db.Database.OpenConnectionAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        AccountLifecycleState state;
        string email;
        await using (var account = await CommandAsync("SELECT lifecycle_state,email FROM `user` WHERE id=@u FOR UPDATE", ct, ("@u", userId)))
        await using (var reader = await account.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                return LifecycleAction.Keep;
            state = Enum.Parse<AccountLifecycleState>(reader.GetString(0));
            email = reader.GetString(1);
        }
        var rows = new List<(Guid Membership, string Status, string Institution, DateTime Joined, string? Role)>();
        await using (var read = await CommandAsync("SELECT m.id,m.status,i.status,m.joined_at,r.role FROM institution_membership m JOIN institution i ON i.id=m.institution_id LEFT JOIN institution_membership_role r ON r.membership_id=m.id WHERE m.user_id=@u", ct, ("@u", userId)))
        await using (var reader = await read.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                rows.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc), reader.IsDBNull(4) ? null : reader.GetString(4)));
        var memberships = rows.GroupBy(r => r.Membership).Select(g => new LifecycleMembership(
            Enum.Parse<MembershipStatus>(g.First().Status, true),
            Enum.Parse<InstitutionStatus>(g.First().Institution, true),
            g.Where(r => r.Role is not null).Select(r => Enum.Parse<InstitutionRole>(r.Role!)).ToArray(),
            g.First().Joined)).ToArray();
        await using var invitation = await CommandAsync("SELECT 1 FROM institution_invitation WHERE recipient_user_id=@u AND status='pending' AND expires_at_utc>UTC_TIMESTAMP(6) LIMIT 1", ct, ("@u", userId));
        var pending = await invitation.ExecuteScalarAsync(ct) is not null;
        var decision = evaluator.Evaluate(new LifecycleSnapshot(state, memberships, pending));
        var applied = decision.Action switch
        {
            LifecycleAction.Close => await writer.CloseAsync(userId, decision.RuleCode!, ct),
            LifecycleAction.Reactivate => await writer.ReactivateAsync(userId, decision.RuleCode!, null, ct),
            _ => false
        };
        if (!applied)
            return LifecycleAction.Keep;
        if (decision.Action == LifecycleAction.Close)
            await mailbox.SuspendAsync(userId, email, ct);
        else
            await mailbox.CreateAsync(userId, email, ct);
        await faults.BeforeCommitAsync(decision.Action == LifecycleAction.Close ? "LifecycleClose" : "LifecycleReactivate", ct);
        await tx.CommitAsync(ct);
        return decision.Action;
    }
}
