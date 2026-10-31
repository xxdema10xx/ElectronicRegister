using ElectronicRegisterAPI.Domain.DTOs.Governance;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class MembershipAuditRepository(ElectronicRegisterContext db) : IMembershipAuditRepository
{
    private IQueryable<InstitutionMembershipEvent> Scoped(TenantContext actor)
    {
        if (actor.SecurityGeneration is null || !actor.Roles.Contains(ElectronicRegisterAPI.Domain.Enums.InstitutionRole.InstitutionAdmin))
            throw new UnauthorizedAccessException();
        var owner = actor.InstitutionId;
        var generation = actor.SecurityGeneration.Value;
        var permitted = db.InstitutionMemberships.Where(m => m.InstitutionId == owner && m.Id == actor.MembershipId && m.UserId == actor.UserId && m.User.Id == actor.UserId && m.Status == "active" && m.Institution.Status == "active" && m.InstitutionMembershipRoles.Any(r => r.Role == "InstitutionAdmin") && db.InstitutionMembershipSecurityStates.Any(s => s.InstitutionId == owner && s.MembershipId == m.Id && s.SecurityGeneration == generation));
        // Couple current privilege validation to the owner-filtered read itself,
        // so revocation after the manager's check cannot expose audit rows.
        return db.Set<InstitutionMembershipEvent>().AsNoTracking().Where(e => e.InstitutionId == owner && permitted.Any());
    }

    private static IQueryable<InstitutionMembershipEvent> Project(IQueryable<InstitutionMembershipEvent> query) => query.Select(e => new InstitutionMembershipEvent { Id = e.Id, ActorUserId = e.ActorUserId, ActorMembershipId = e.ActorMembershipId, TargetMembershipId = e.TargetMembershipId, Action = e.Action, TargetRole = e.TargetRole, PreviousStatus = e.PreviousStatus, NewStatus = e.NewStatus, PreviousRoles = e.PreviousRoles, NewRoles = e.NewRoles, Reason = e.Reason, RevisionBefore = e.RevisionBefore, RevisionAfter = e.RevisionAfter, OccurredAtUtc = e.OccurredAtUtc });
    private static MembershipAuditEntry Map(InstitutionMembershipEvent e) => new(e.Id, e.ActorUserId, e.ActorMembershipId, e.TargetMembershipId, e.Action, e.TargetRole, e.PreviousStatus, e.NewStatus, JsonSerializer.Deserialize<string[]>(e.PreviousRoles)!, JsonSerializer.Deserialize<string[]>(e.NewRoles)!, e.Reason, e.RevisionBefore, e.RevisionAfter, DateTime.SpecifyKind(e.OccurredAtUtc, DateTimeKind.Utc));
    public async Task<IReadOnlyList<MembershipAuditEntry>> ListAsync(
        TenantContext actor,
        int page,
        int size,
        Guid? target,
        string? action,
        CancellationToken ct
    )
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
            throw new ArgumentException();
        var query = Scoped(actor);
        if (target.HasValue)
            query = query.Where(e => e.TargetMembershipId == target.Value);
        if (action is not null)
            query = query.Where(e => e.Action == action);
        // Projection excludes RequestHash and any technical provisioning ledger.
        var rows = await Project(query.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return rows.Select(Map).ToArray();
    }

    public async Task<MembershipAuditEntry?> GetAsync(TenantContext actor, Guid eventId, CancellationToken ct)
    {
        var row = await Project(Scoped(actor).Where(e => e.Id == eventId)).SingleOrDefaultAsync(ct);
        return row is null ? null : Map(row);
    }
}
