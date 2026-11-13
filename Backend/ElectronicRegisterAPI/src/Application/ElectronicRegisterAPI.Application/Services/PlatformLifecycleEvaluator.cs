using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;

namespace ElectronicRegisterAPI.Application.Services;

internal sealed class PlatformLifecycleEvaluator(PlatformIdentitySettings settings, TimeProvider clock) : ILifecycleEvaluator
{
    public LifecycleDecision Evaluate(LifecycleSnapshot snapshot)
    {
        settings.RequireEnabled();
        return PlatformLifecycleRules.Evaluate(snapshot, clock.GetUtcNow().UtcDateTime, settings.StudentClosureAfterMonths);
    }

    // Students whose earliest Student membership started at or before this instant are past
    // the configured period; used only to pre-filter candidates, never to decide.
    public DateTime StudentStartedBeforeUtc()
    {
        settings.RequireEnabled();
        return clock.GetUtcNow().UtcDateTime.AddMonths(-settings.StudentClosureAfterMonths);
    }
}
