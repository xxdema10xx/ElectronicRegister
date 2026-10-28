using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionWriteAuthorization(ElectronicRegisterContext db, IMembershipAuthorizationRepository authorization) : IInstitutionWriteAuthorization
{
    private Guid? transactionId;
    private TenantContext? lockedActor;
    public void RequireLocked(TenantContext actor)
    {
        if (transactionId is null || db.Database.CurrentTransaction?.TransactionId != transactionId || lockedActor is null || lockedActor.UserId != actor.UserId || lockedActor.InstitutionId != actor.InstitutionId || lockedActor.MembershipId != actor.MembershipId || lockedActor.SecurityGeneration != actor.SecurityGeneration || !lockedActor.Roles.SetEquals(actor.Roles))
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
        var value = await authorization.GetAsync(actor.UserId, actor.InstitutionId, actor.MembershipId, ct);
        if (value is null || value.Status != MembershipStatus.Active || value.InstitutionStatus != InstitutionStatus.Active || value.UserId != actor.UserId || value.InstitutionId != actor.InstitutionId || value.MembershipId != actor.MembershipId || value.SecurityGeneration is null or < 0 || value.SecurityGeneration != actor.SecurityGeneration || !actor.Roles.SetEquals(value.Roles) || value.Roles.Any(r => !Enum.IsDefined(r)))
            throw new UnauthorizedAccessException("Membership istituzionale non autorizzata.");
        transactionId = db.Database.CurrentTransaction.TransactionId;
        lockedActor = actor;
    }
}
