using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionWriteAuthorization(ElectronicRegisterContext db, IIdentityAccountRepository accounts) : IInstitutionWriteAuthorization
{
    private Guid? transactionId;
    private TenantContext? lockedActor;
    public void RequireLocked(TenantContext actor)
    {
        if (transactionId is null || db.Database.CurrentTransaction?.TransactionId != transactionId || lockedActor is null || lockedActor.UserId != actor.UserId || lockedActor.InstitutionId != actor.InstitutionId || lockedActor.MembershipId != actor.MembershipId || lockedActor.SecurityGeneration != actor.SecurityGeneration || lockedActor.IdentityGeneration != actor.IdentityGeneration || !lockedActor.Roles.SetEquals(actor.Roles))
            throw new UnauthorizedAccessException("Autorizzazione transazionale richiesta.");
    }

    public async Task LockAsync(TenantContext actor, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Transazione richiesta.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={actor.InstitutionId} LOCK IN SHARE MODE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution_membership WHERE institution_id={actor.InstitutionId} AND id={actor.MembershipId} LOCK IN SHARE MODE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT r.role FROM institution_membership_role r JOIN institution_membership m ON m.id=r.membership_id WHERE m.institution_id={actor.InstitutionId} AND m.id={actor.MembershipId} ORDER BY r.role LOCK IN SHARE MODE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT membership_id FROM institution_membership_security_state WHERE institution_id={actor.InstitutionId} AND membership_id={actor.MembershipId} LOCK IN SHARE MODE", ct);
        // HTTP contexts carry the authenticated global generation. The identity
        // lock follows IAM locks, as in recipient acceptance; password rotation
        // only locks identity rows and cannot invert the tenant lock order.
        if (actor.IdentityGeneration is { } identityGeneration)
            await accounts.RequireGenerationAsync(actor.UserId, identityGeneration, ct);
        var value = await ReadLockedAsync(actor, ct);
        if (!MembershipAuthorizationRules.Matches(actor, value))
            throw new UnauthorizedAccessException("Membership istituzionale non autorizzata.");
        transactionId = db.Database.CurrentTransaction.TransactionId;
        lockedActor = actor;
    }

    private async Task<MembershipAuthorization?> ReadLockedAsync(TenantContext actor, CancellationToken ct)
    {
        // A consistent EF projection here would establish a REPEATABLE READ
        // snapshot BEFORE a later resource lock waits for the expiry worker.
        // Keep authorization a current locking read; resource reads must see the
        // winner that committed while their row lock was waiting.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT m.user_id,i.name,i.status,m.status,s.security_generation,r.role FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN `user` u ON u.id=m.user_id LEFT JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id LEFT JOIN institution_membership_role r ON r.membership_id=m.id WHERE m.institution_id=@institution AND m.id=@membership AND m.user_id=@user LOCK IN SHARE MODE";
        foreach (var (name, value) in new[] { ("@institution", actor.InstitutionId), ("@membership", actor.MembershipId), ("@user", actor.UserId) })
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        var user = reader.GetGuid(0);
        var institutionName = reader.GetString(1);
        var institutionStatus = Enum.Parse<InstitutionStatus>(reader.GetString(2), true);
        var status = Enum.Parse<MembershipStatus>(reader.GetString(3), true);
        long? generation = reader.IsDBNull(4) ? null : reader.GetInt64(4);
        var roles = new List<InstitutionRole>();
        do
        {
            if (!reader.IsDBNull(5))
                roles.Add(Enum.Parse<InstitutionRole>(reader.GetString(5)));
        } while (await reader.ReadAsync(ct));
        return new(user, actor.InstitutionId, actor.MembershipId, institutionName, institutionStatus, status, roles, generation);
    }

    public async Task LockExamAsync(TenantContext actor, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Transazione richiesta.");
        // Same tenant-first order as IAM. An exclusive mutex avoids S->X upgrades
        // by exam creation and makes revocation/resource commits linearizable.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={actor.InstitutionId} FOR UPDATE", ct);
        await LockAsync(actor, ct);
    }
}
