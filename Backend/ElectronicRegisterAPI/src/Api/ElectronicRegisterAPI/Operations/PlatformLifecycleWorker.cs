using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;

namespace ElectronicRegisterAPI.Api.Operations;

// Account lifecycle maintenance (er-platform-identity-1): closes accounts without active
// memberships according to the role rules and, as a safety net, reopens Closed accounts that
// have an active membership again. Active only with Multitenancy:PlatformIdentity:Enabled.
// Each pass uses a new DI scope; every account is its own idempotent transaction.
internal sealed class PlatformLifecycleWorker(PlatformIdentitySettings settings, IServiceScopeFactory scopes, ILogger<PlatformLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
            return;
        using var timer = new PeriodicTimer(settings.LifecycleInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IPlatformLifecycleManager>().RunPassAsync(stoppingToken);
                if (result.Failed > 0)
                    logger.LogWarning("Lifecycle pass completed with {Failed} failed accounts ({ErrorTypes}); they will be retried.", result.Failed, string.Join(",", result.FailureTypes.Distinct(StringComparer.Ordinal)));
                else if (result.Closed + result.Reactivated > 0)
                    logger.LogInformation("Lifecycle pass closed {Closed} and reactivated {Reactivated} accounts.", result.Closed, result.Reactivated);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                logger.LogWarning("Lifecycle pass failed ({ErrorType}); will retry.", error.GetType().Name);
            }
        }
    }
}
