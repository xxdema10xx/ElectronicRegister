using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

// One membership of a global identity, read under the identity row lock.
public sealed record LifecycleMembership(
    MembershipStatus Status,
    InstitutionStatus InstitutionStatus,
    IReadOnlyCollection<InstitutionRole> Roles,
    DateTime JoinedAtUtc
);
