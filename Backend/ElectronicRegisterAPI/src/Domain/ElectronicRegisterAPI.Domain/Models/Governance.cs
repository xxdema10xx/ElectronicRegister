using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Models;

public interface IInstitutionGovernanceRepository
{
    Task<PlatformResult> ExecuteAsync(SignedPlatformOperation operation, CancellationToken ct);
}
public interface IInstitutionGovernanceManager
{
    Task<PlatformResult> ExecuteAsync(SignedPlatformOperation operation, CancellationToken ct = default);
}
public sealed record MembershipAuditEntry(Guid EventId, Guid ActorUserId, Guid ActorMembershipId,
    Guid TargetMembershipId, string Action, string? Role, string PreviousStatus, string NewStatus,
    string[] PreviousRoles, string[] NewRoles, string Reason, long RevisionBefore, long RevisionAfter, DateTime OccurredAtUtc);
public interface IMembershipAuditRepository
{
    Task<IReadOnlyList<MembershipAuditEntry>> ListAsync(TenantContext actor, int page, int size, Guid? target, string? action, CancellationToken ct);
    Task<MembershipAuditEntry?> GetAsync(TenantContext actor, Guid eventId, CancellationToken ct);
}
public interface IMembershipAuditManager
{
    Task<IReadOnlyList<MembershipAuditEntry>> ListAsync(int page = 1, int size = 20, Guid? target = null, string? action = null, CancellationToken ct = default);
    Task<MembershipAuditEntry> GetAsync(Guid eventId, CancellationToken ct = default);
}
