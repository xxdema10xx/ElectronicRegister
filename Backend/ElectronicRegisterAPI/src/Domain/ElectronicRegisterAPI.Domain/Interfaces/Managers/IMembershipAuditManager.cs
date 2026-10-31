using ElectronicRegisterAPI.Domain.DTOs.Governance;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IMembershipAuditManager
{
    Task<IReadOnlyList<MembershipAuditEntry>> ListAsync(
        int page = 1,
        int size = 20,
        Guid? target = null,
        string? action = null,
        CancellationToken ct = default
    );
    Task<MembershipAuditEntry> GetAsync(Guid eventId, CancellationToken ct = default);
}
