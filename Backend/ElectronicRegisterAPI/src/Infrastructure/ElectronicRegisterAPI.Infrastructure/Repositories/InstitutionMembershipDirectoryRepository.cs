using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Member = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.InstitutionMembership;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionMembershipDirectoryRepository(ElectronicRegisterContext db) : IInstitutionMembershipDirectoryRepository
{
    public async Task<IReadOnlyList<ElectronicRegisterAPI.Domain.Results.ProfileAcquisitionResult>> AcquisitionsAsync(
        Guid institutionId,
        Guid membershipId,
        int page,
        int size,
        string? status,
        CancellationToken ct
    )
    {
        if (institutionId == Guid.Empty || page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue || status is not (null or "linked" or "requires-review"))
            throw new ArgumentException();
        var offset = (page - 1) * size;
        return await db.Database.SqlQuery<ElectronicRegisterAPI.Domain.Results.ProfileAcquisitionResult>($"SELECT id AS EventId,target_membership_id AS MembershipId,COALESCE(student_id,teacher_id) AS ProfileId,IF(student_id IS NOT NULL,'Student','Teacher') AS Kind,status AS Status FROM institution_profile_acquisition WHERE institution_id={institutionId} AND target_membership_id={membershipId} AND ({status} IS NULL OR status={status}) ORDER BY created_at DESC,id LIMIT {size} OFFSET {offset}").ToListAsync(ct);
    }

    private IQueryable<Member> Scoped(Guid institutionId)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Tenant obbligatorio.");
        return db.InstitutionMemberships.AsNoTracking().Where(m => m.InstitutionId == institutionId);
    }

    public async Task<IReadOnlyList<InstitutionMemberDto>> ListAsync(
        Guid institutionId,
        int pageNumber,
        int pageSize,
        string? status,
        string? role,
        string? search,
        CancellationToken ct
    )
    {
        if (pageNumber < 1 || pageSize is < 1 or > 100 || (long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new ArgumentException("Paginazione non valida.");
        var query = Scoped(institutionId);
        if (status is not null)
            query = query.Where(m => m.Status == status);
        if (role is not null)
            query = query.Where(m => m.InstitutionMembershipRoles.Any(r => r.Role == role));
        // EF translates a literal substring, scoped to existing members.
        if (search is not null)
            query = query.Where(m => m.User.Email.Contains(search));
        return await ReadAsync(query.OrderBy(m => m.JoinedAt).ThenBy(m => m.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize), ct);
    }

    public async Task<InstitutionMemberDto?> GetAsync(Guid institutionId, Guid membershipId, CancellationToken ct) => (await ReadAsync(Scoped(institutionId).Where(m => m.Id == membershipId), ct)).SingleOrDefault();
    private static async Task<IReadOnlyList<InstitutionMemberDto>> ReadAsync(IQueryable<Member> query, CancellationToken ct)
    {
        var rows = await query.AsSingleQuery().Select(m => new { MembershipId = m.Id, m.UserId, m.User.Email, m.Status, m.JoinedAt, m.SuspendedAt, Roles = m.InstitutionMembershipRoles.OrderBy(r => r.Role).Select(r => r.Role).ToList() }).ToListAsync(ct);
        return rows.Select(m => new InstitutionMemberDto(m.MembershipId, m.UserId, m.Email, m.Status, m.Roles.Order(StringComparer.Ordinal).ToArray(), DateTime.SpecifyKind(m.JoinedAt, DateTimeKind.Utc), m.SuspendedAt.HasValue ? DateTime.SpecifyKind(m.SuspendedAt.Value, DateTimeKind.Utc) : null)).ToArray();
    }
}
