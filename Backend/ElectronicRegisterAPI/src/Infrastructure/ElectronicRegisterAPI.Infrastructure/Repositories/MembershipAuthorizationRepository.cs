using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class MembershipAuthorizationRepository(ElectronicRegisterContext db) : IMembershipAuthorizationRepository
{
    public Task<IReadOnlyList<MembershipAuthorization>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ReadAsync(userId, null, null, cancellationToken);

    public async Task<MembershipAuthorization?> GetAsync(Guid userId, Guid institutionId, Guid membershipId, CancellationToken cancellationToken = default) =>
        (await ReadAsync(userId, institutionId, membershipId, cancellationToken)).SingleOrDefault();

    private async Task<IReadOnlyList<MembershipAuthorization>> ReadAsync(Guid userId, Guid? institutionId, Guid? membershipId, CancellationToken ct)
    {
        // One SQL statement/snapshot, no tracked state or split queries: current user, parent and roles.
        var rows = await db.InstitutionMemberships.AsNoTracking().AsSingleQuery()
            .Where(m => m.UserId == userId && m.User.Id == userId &&
                (!institutionId.HasValue || m.InstitutionId == institutionId.Value) &&
                (!membershipId.HasValue || m.Id == membershipId.Value))
            .Select(m => new { m.Id, m.UserId, m.InstitutionId, m.Status, Name = m.Institution.Name,
                InstitutionStatus = m.Institution.Status,
                Generation = db.InstitutionMembershipSecurityStates
                    .Where(s => s.InstitutionId == m.InstitutionId && s.MembershipId == m.Id)
                    .Select(s => (long?)s.SecurityGeneration).FirstOrDefault(),
                Roles = m.InstitutionMembershipRoles.Select(r => r.Role).ToList() })
            .ToListAsync(ct);
        return rows.Select(m => new MembershipAuthorization(m.UserId, m.InstitutionId, m.Id, m.Name,
            Enum.Parse<InstitutionStatus>(m.InstitutionStatus, true), Enum.Parse<MembershipStatus>(m.Status, true),
            m.Roles.Select(r => Enum.Parse<InstitutionRole>(r)).ToArray(), m.Generation)).ToArray();
    }
}
