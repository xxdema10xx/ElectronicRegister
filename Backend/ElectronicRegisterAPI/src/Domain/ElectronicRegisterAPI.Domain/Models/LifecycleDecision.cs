using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

// RuleCode is set for Close/Reactivate; NotBeforeUtc explains a kept student account.
public sealed record LifecycleDecision(
    LifecycleAction Action,
    string? RuleCode,
    DateTime? NotBeforeUtc = null
);
