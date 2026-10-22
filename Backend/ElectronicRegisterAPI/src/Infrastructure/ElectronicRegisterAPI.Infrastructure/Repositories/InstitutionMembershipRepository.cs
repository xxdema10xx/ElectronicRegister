using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Entity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.InstitutionMembership;
using RoleEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.InstitutionMembershipRole;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionMembershipRepository(ElectronicRegisterContext context) : IInstitutionMembershipRepository
{
    public async Task<InstitutionMembership?> GetAsync(Guid institutionId, Guid userId)
    {
        var row = await context.InstitutionMemberships.AsNoTracking().Include(m => m.InstitutionMembershipRoles)
            .SingleOrDefaultAsync(m => m.InstitutionId == institutionId && m.UserId == userId);
        return row is null ? null : new InstitutionMembership
        {
            Id = row.Id, InstitutionId = row.InstitutionId, UserId = row.UserId,
            Status = Enum.Parse<MembershipStatus>(row.Status, true), JoinedAt = row.JoinedAt, SuspendedAt = row.SuspendedAt,
            Roles = row.InstitutionMembershipRoles.Select(r => Enum.Parse<InstitutionRole>(r.Role)).ToList()
        };
    }

    public async Task AddAsync(InstitutionMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        if (membership.Id == Guid.Empty || membership.InstitutionId == Guid.Empty || membership.UserId == Guid.Empty ||
            !Enum.IsDefined(membership.Status) || membership.Roles is null || membership.Roles.Any(r => !Enum.IsDefined(r)) ||
            membership.Roles.Distinct().Count() != membership.Roles.Count)
            throw new ArgumentException("Invalid membership identity/status/roles.");
        if (membership.Status == MembershipStatus.Suspended && !membership.SuspendedAt.HasValue)
            throw new ArgumentException("Suspended membership requires a timestamp.");
        await using var transaction = await context.Database.BeginTransactionAsync();
        await RequireProvisioningUnderLockAsync(membership.InstitutionId);
        context.InstitutionMemberships.Add(new Entity
        {
            Id = membership.Id, InstitutionId = membership.InstitutionId, UserId = membership.UserId,
            Status = membership.Status.ToString().ToLowerInvariant(), JoinedAt = membership.JoinedAt == default ? DateTime.UtcNow : membership.JoinedAt,
            SuspendedAt = membership.SuspendedAt,
            InstitutionMembershipRoles = membership.Roles.Select(r => new RoleEntity { MembershipId = membership.Id, Role = r.ToString() }).ToList()
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task AddRoleAsync(Guid institutionId, Guid membershipId, InstitutionRole role)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentException("Unknown institution role.");
        await using var transaction = await context.Database.BeginTransactionAsync();
        await RequireProvisioningUnderLockAsync(institutionId);
        if (!await context.InstitutionMemberships.AnyAsync(m => m.Id == membershipId && m.InstitutionId == institutionId))
            throw new KeyNotFoundException("Membership not found in the specified institution.");
        context.InstitutionMembershipRoles.Add(new RoleEntity { MembershipId = membershipId, Role = role.ToString() });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task SuspendAsync(Guid institutionId, Guid membershipId, DateTime suspendedAtUtc)
    {
        if (suspendedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC timestamp required.");
        await using var transaction = await context.Database.BeginTransactionAsync();
        await RequireProvisioningUnderLockAsync(institutionId);
        var changed = await context.InstitutionMemberships.Where(m => m.Id == membershipId && m.InstitutionId == institutionId)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.Status, "suspended").SetProperty(x => x.SuspendedAt, suspendedAtUtc));
        if (changed == 0) throw new KeyNotFoundException("Membership not found in the specified institution.");
        await transaction.CommitAsync();
    }

    // Technical provisioning only, never an interactive IAM mutation or a source of
    // human actor attribution. First locking read is the per-Institution mutex.
    private async Task RequireProvisioningUnderLockAsync(Guid institutionId)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={institutionId} FOR UPDATE");
        var state = await context.Institutions.AsNoTracking().Where(i => i.Id == institutionId).Select(i => i.Status).SingleOrDefaultAsync();
        if (state is null) throw new KeyNotFoundException("Institution not found.");
        if (state != "provisioning") throw new UnauthorizedAccessException("Il writer tecnico richiede Institution Provisioning.");
        // Verified onboarding has a separate attributed writer. Legacy preparation
        // cannot pre-seed additional administrators before first acceptance.
        if(await context.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM institution_provisioning WHERE institution_id={institutionId}").SingleAsync()!=0)
            throw new UnauthorizedAccessException("Onboarding verificato richiede il proprio writer autorizzato.");
    }
}
