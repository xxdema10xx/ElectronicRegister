namespace ElectronicRegisterAPI.Domain.Models;

// FailureTypes holds exception type names only, never messages or account data.
public sealed record LifecyclePassResult(
    int Examined,
    int Closed,
    int Reactivated,
    int Failed,
    IReadOnlyList<string> FailureTypes
);
