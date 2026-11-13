using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record LifecycleSnapshot(
    AccountLifecycleState State,
    IReadOnlyList<LifecycleMembership> Memberships,
    bool HasPendingInvitation
);
