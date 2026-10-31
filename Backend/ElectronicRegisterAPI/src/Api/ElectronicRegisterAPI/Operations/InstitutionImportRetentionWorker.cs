using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Api.Operations;

// This is retention maintenance only. Imports themselves are synchronous and never return 202.
internal sealed class InstitutionImportRetentionWorker(IServiceScopeFactory scopes, ILogger<InstitutionImportRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IInstitutionImportRetention>().PurgeExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                logger.LogWarning("Import retention pass failed ({ErrorType}); will retry.", error.GetType().Name);
            }
        }
    }
}
