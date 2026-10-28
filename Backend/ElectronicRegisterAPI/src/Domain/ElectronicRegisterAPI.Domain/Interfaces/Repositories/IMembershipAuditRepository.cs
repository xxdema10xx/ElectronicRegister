using ElectronicRegisterAPI.Domain.DTOs.Governance;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IMembershipAuditRepository
{
    Task<IReadOnlyList<MembershipAuditEntry>> ListAsync(
        TenantContext actor,
        int page,
        int size,
        Guid? target,
        string? action,
        CancellationToken ct
    );
    Task<MembershipAuditEntry?> GetAsync(TenantContext actor, Guid eventId, CancellationToken ct);
}
