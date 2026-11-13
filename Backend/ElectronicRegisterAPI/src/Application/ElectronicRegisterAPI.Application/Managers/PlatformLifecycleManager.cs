using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

// One lifecycle pass: safety-net reactivations first, then closures, each bounded by the
// configured batch size and paginated by user id. Every account is applied in its own
// transaction; a failing account is counted and never blocks the others.
internal sealed class PlatformLifecycleManager(PlatformIdentitySettings settings, IPlatformLifecycleRepository repository, ILifecycleEvaluator evaluator) : IPlatformLifecycleManager
{
    public async Task<LifecyclePassResult> RunPassAsync(CancellationToken ct)
    {
        if (!settings.Enabled || !await repository.InstalledAsync(ct))
            return new(0, 0, 0, 0, []);
        var tally = new int[4];
        var failures = new List<string>();
        await ProcessAsync((after, limit) => repository.ReactivationCandidatesAsync(after, limit, ct), tally, failures, ct);
        var cutoff = evaluator.StudentStartedBeforeUtc();
        await ProcessAsync((after, limit) => repository.ClosureCandidatesAsync(after, limit, cutoff, ct), tally, failures, ct);
        return new(tally[0], tally[1], tally[2], tally[3], failures);
    }

    private async Task ProcessAsync(
        Func<Guid?, int, Task<IReadOnlyList<Guid>>> page,
        int[] tally,
        List<string> failures,
        CancellationToken ct
    )
    {
        Guid? after = null;
        var budget = settings.LifecycleBatchSize;
        while (budget > 0)
        {
            ct.ThrowIfCancellationRequested();
            var ids = await page(after, budget);
            if (ids.Count == 0)
                return;
            foreach (var id in ids)
            {
                tally[0]++;
                try
                {
                    var action = await repository.ApplyAsync(id, ct);
                    if (action == LifecycleAction.Close)
                        tally[1]++;
                    else if (action == LifecycleAction.Reactivate)
                        tally[2]++;
                }
                catch (Exception error) when (!ct.IsCancellationRequested)
                {
                    tally[3]++;
                    failures.Add(error.GetType().Name);
                }
            }
            budget -= ids.Count;
            after = ids[^1];
        }
    }
}
