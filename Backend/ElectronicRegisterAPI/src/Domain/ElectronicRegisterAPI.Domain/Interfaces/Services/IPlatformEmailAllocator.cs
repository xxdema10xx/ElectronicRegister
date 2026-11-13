using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

// Inside the caller's transaction: takes the global email mutex and loads, in bulk, every
// reserved address that may collide with the given bases. Uniqueness itself remains the
// reservation primary key; callers retry with the next candidate on a 1062.
public interface IPlatformEmailAllocator
{
    Task<PlatformEmailPlan> PlanAsync(IReadOnlyCollection<string> localBases, CancellationToken ct);
}
