using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Governance;

public sealed record MembershipAuditEntry(
    Guid EventId,
    Guid ActorUserId,
    Guid ActorMembershipId,
    Guid TargetMembershipId,
    string Action,
    string? Role,
    string PreviousStatus,
    string NewStatus,
    string[] PreviousRoles,
    string[] NewRoles,
    string Reason,
    long RevisionBefore,
    long RevisionAfter,
    DateTime OccurredAtUtc
);
