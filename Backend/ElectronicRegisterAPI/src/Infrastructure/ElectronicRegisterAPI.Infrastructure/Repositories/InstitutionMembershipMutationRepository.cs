using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionMembershipMutationRepository(ElectronicRegisterContext db,
    IMembershipAuthorizationRepository authorization) : IInstitutionMembershipMutationRepository
{
    public async Task<MembershipMutationResult> MutateAsync(TenantContext actor, MembershipMutation request, CancellationToken ct = default)
    {
        MembershipMutationRules.Validate(request);
        // Canonical typed intent; do not include JWT generation (a fresh authorized token may retry).
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            actor.InstitutionId,actor.UserId,ActorMembershipId=actor.MembershipId,request.MembershipId,
            request.EventId,request.ExpectedRevision,Action=request.Action.ToString(),Role=request.Role?.ToString(),request.Reason }))));
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        // First database statement in the transaction is the tenant mutex. Never reuse a prior read snapshot.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={actor.InstitutionId} FOR UPDATE",ct);
        foreach(var id in new[]{actor.MembershipId,request.MembershipId}.Distinct().OrderBy(x=>x.ToString("D"),StringComparer.Ordinal))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution_membership WHERE institution_id={actor.InstitutionId} AND id={id} FOR UPDATE",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT r.role FROM institution_membership_role r JOIN institution_membership m ON m.id=r.membership_id WHERE m.institution_id={actor.InstitutionId} AND m.id={id} ORDER BY r.role FOR UPDATE",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT membership_id FROM institution_membership_security_state WHERE institution_id={actor.InstitutionId} AND membership_id={id} FOR UPDATE",ct);
        }
        // No tracked entities or pre-lock authorization are trusted, even for a persisted replay.
        var current=await authorization.GetAsync(actor.UserId,actor.InstitutionId,actor.MembershipId,ct);
        if(!IsCurrentAdmin(actor,current))
            throw new UnauthorizedAccessException("Amministratore istituzionale non autorizzato.");
        if(actor.MembershipId==request.MembershipId) throw new UnauthorizedAccessException("Autoamministrazione non consentita.");
        var prior=await db.InstitutionMembershipEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.InstitutionId==actor.InstitutionId && e.Id==request.EventId,ct);
        // Detect conflicting EventId reuse independently of whether the proposed new
        // target exists. Neither a foreign nor an absent ID can change this outcome.
        if(prior is not null && prior.RequestHash!=hash) throw new MembershipConflictException();
        var target=await db.InstitutionMemberships.AsNoTracking().Where(m=>m.InstitutionId==actor.InstitutionId && m.Id==request.MembershipId)
            .Select(m=>new{m.Status,Roles=m.InstitutionMembershipRoles.Select(r=>r.Role).ToList()}).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Membership non disponibile.");
        var revision=await db.InstitutionMembershipSecurityStates.AsNoTracking().Where(s=>s.InstitutionId==actor.InstitutionId && s.MembershipId==request.MembershipId)
            .Select(s=>(long?)s.SecurityGeneration).SingleOrDefaultAsync(ct);
        if(revision is null || revision<0) throw new UnauthorizedAccessException("Stato di sicurezza assente.");
        if(prior is not null)
        {
            await tx.CommitAsync(ct);
            return Result(prior.Id,prior.RevisionAfter,prior.NewStatus,JsonSerializer.Deserialize<string[]>(prior.NewRoles)!);
        }
        if(revision!=request.ExpectedRevision || revision==long.MaxValue) throw new MembershipConflictException();
        var status=Enum.Parse<MembershipStatus>(target.Status,true);
        var roles=target.Roles.Select(Enum.Parse<InstitutionRole>).OrderBy(x=>x.ToString(),StringComparer.Ordinal).ToArray();
        MembershipMutationRules.RequireTransition(request,status,roles);
        var removesAdmin=status==MembershipStatus.Active && roles.Contains(InstitutionRole.InstitutionAdmin) &&
            (request.Action==MembershipMutationAction.Suspend || request.Action==MembershipMutationAction.RemoveRole && request.Role==InstitutionRole.InstitutionAdmin);
        if(removesAdmin)
        {
            var others=await db.InstitutionMemberships.AsNoTracking().LongCountAsync(m=>m.InstitutionId==actor.InstitutionId && m.Id!=request.MembershipId &&
                m.Status=="active" && m.User.Id==m.UserId && m.InstitutionMembershipRoles.Any(r=>r.Role=="InstitutionAdmin"),ct);
            MembershipMutationRules.RequireLastAdmin(true,others);
        }
        int affected;
        switch(request.Action)
        {
            case MembershipMutationAction.AddRole:
                affected=await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO institution_membership_role(membership_id,role) VALUES({request.MembershipId},{request.Role!.Value.ToString()})",ct);break;
            case MembershipMutationAction.RemoveRole:
                affected=await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM institution_membership_role WHERE membership_id={request.MembershipId} AND role={request.Role!.Value.ToString()}",ct);break;
            case MembershipMutationAction.Suspend:
                affected=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE institution_membership SET status='suspended',suspended_at=UTC_TIMESTAMP(6) WHERE institution_id={actor.InstitutionId} AND id={request.MembershipId}",ct);break;
            case MembershipMutationAction.Reactivate:
                affected=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE institution_membership SET status='active',suspended_at=NULL WHERE institution_id={actor.InstitutionId} AND id={request.MembershipId}",ct);break;
            default:
                throw new ArgumentException("Azione IAM non supportata.");
        }
        if(affected!=1) throw new MembershipConflictException();
        var after=await db.InstitutionMembershipSecurityStates.AsNoTracking().Where(s=>s.InstitutionId==actor.InstitutionId && s.MembershipId==request.MembershipId)
            .Select(s=>s.SecurityGeneration).SingleAsync(ct);
        if(after!=revision+1) throw new InvalidOperationException("Avanzamento della generation non valido.");
        var newStatus=request.Action==MembershipMutationAction.Suspend?"suspended":request.Action==MembershipMutationAction.Reactivate?"active":target.Status;
        var newRoles=roles.ToHashSet();
        if(request.Action==MembershipMutationAction.AddRole)newRoles.Add(request.Role!.Value);
        if(request.Action==MembershipMutationAction.RemoveRole)newRoles.Remove(request.Role!.Value);
        var beforeJson=JsonSerializer.Serialize(roles.Select(x=>x.ToString()));
        var afterJson=JsonSerializer.Serialize(newRoles.Select(x=>x.ToString()).Order(StringComparer.Ordinal));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO institution_membership_event(institution_id,id,actor_user_id,actor_membership_id,target_membership_id,action,target_role,previous_status,new_status,previous_roles,new_roles,reason,revision_before,revision_after,request_hash,occurred_at_utc) VALUES({actor.InstitutionId},{request.EventId},{actor.UserId},{actor.MembershipId},{request.MembershipId},{request.Action.ToString()},{(request.Role.HasValue?request.Role.Value.ToString():null)},{target.Status},{newStatus},{beforeJson},{afterJson},{request.Reason},{revision},{after},{hash},UTC_TIMESTAMP(6))",ct);
        await tx.CommitAsync(ct);
        return Result(request.EventId,after,newStatus,JsonSerializer.Deserialize<string[]>(afterJson)!);
    }
    private static MembershipMutationResult Result(Guid id,long revision,string status,IEnumerable<string> roles) =>
        new(id,revision,Enum.Parse<MembershipStatus>(status,true),roles.Select(Enum.Parse<InstitutionRole>).OrderBy(x=>x.ToString(),StringComparer.Ordinal).ToArray());

    private static bool IsCurrentAdmin(TenantContext actor,MembershipAuthorization? current) =>
        current is not null && current.UserId==actor.UserId && current.InstitutionId==actor.InstitutionId &&
        current.MembershipId==actor.MembershipId && current.Status==MembershipStatus.Active &&
        current.InstitutionStatus==InstitutionStatus.Active && current.SecurityGeneration is >=0 &&
        actor.SecurityGeneration==current.SecurityGeneration && actor.Roles.SetEquals(current.Roles) &&
        current.Roles.All(Enum.IsDefined) && current.Roles.Contains(InstitutionRole.InstitutionAdmin);
}
