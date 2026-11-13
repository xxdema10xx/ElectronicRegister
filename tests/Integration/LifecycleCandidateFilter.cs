using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Tests.Integration;

// Test-only decorator: the real candidate SQL runs, but only accounts created by the current
// test class are handed to the manager, so a lifecycle pass never closes users that other
// test classes left in the shared integration database. ApplyAsync is the real repository.
internal sealed class LifecycleCandidateFilter(IPlatformLifecycleRepository inner, ConcurrentDictionary<Guid, bool> allowed) : IPlatformLifecycleRepository
{
    public Task<bool> InstalledAsync(CancellationToken ct) => inner.InstalledAsync(ct);

    public async Task<IReadOnlyList<Guid>> ClosureCandidatesAsync(
        Guid? after,
        int limit,
        DateTime studentStartedBeforeUtc,
        CancellationToken ct
    ) => (await inner.ClosureCandidatesAsync(after, int.MaxValue / 2, studentStartedBeforeUtc, ct)).Where(allowed.ContainsKey).Take(limit).ToArray();

    public async Task<IReadOnlyList<Guid>> ReactivationCandidatesAsync(Guid? after, int limit, CancellationToken ct) =>
        (await inner.ReactivationCandidatesAsync(after, int.MaxValue / 2, ct)).Where(allowed.ContainsKey).Take(limit).ToArray();

    public Task<LifecycleAction> ApplyAsync(Guid userId, CancellationToken ct) => inner.ApplyAsync(userId, ct);
}
