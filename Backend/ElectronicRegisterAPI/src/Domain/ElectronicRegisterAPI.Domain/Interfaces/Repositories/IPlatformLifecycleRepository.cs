using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

// Global identity maintenance: no tenant authority is consulted or inferred. Every Apply is
// its own transaction that re-reads the account under lock and is idempotent.
public interface IPlatformLifecycleRepository
{
    Task<bool> InstalledAsync(CancellationToken ct);
    Task<IReadOnlyList<Guid>> ClosureCandidatesAsync(
        Guid? after,
        int limit,
        DateTime studentStartedBeforeUtc,
        CancellationToken ct
    );
    Task<IReadOnlyList<Guid>> ReactivationCandidatesAsync(Guid? after, int limit, CancellationToken ct);
    Task<LifecycleAction> ApplyAsync(Guid userId, CancellationToken ct);
}
