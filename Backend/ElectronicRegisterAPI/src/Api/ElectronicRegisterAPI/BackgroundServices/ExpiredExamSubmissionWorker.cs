using ElectronicRegisterAPI.Domain.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace ElectronicRegisterAPI.Api.BackgroundServices;

internal class ExpiredExamSubmissionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredExamSubmissionWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(15);
    private readonly int _batchSize = 50;
    private readonly IConfiguration _configuration;
    private ExpiryCandidate? _cursor;
    private DateTime? _sweepUntil;
    private readonly SemaphoreSlim _cycle = new(1, 1);
    public ExpiredExamSubmissionWorker(IServiceScopeFactory scopeFactory, ILogger<ExpiredExamSubmissionWorker> logger, IConfiguration? configuration = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration ?? new ConfigurationBuilder().Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredExamSubmissionWorker started.");
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Expired submissions batch failed ({ErrorType}).", ex.GetType().Name);
                }
            }
        }
        finally
        {
            _logger.LogInformation("ExpiredExamSubmissionWorker stopping.");
        }
    }

    // The timer and integration tests invoke the same selection/dispatch path.
    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await _cycle.WaitAsync(cancellationToken);
        try
        {
            // A legacy scan failure must not suppress the independent institutional lane.
            try
            {
                await ProcessLegacyBatchAsync(cancellationToken);
            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Legacy expiry scan failed ({ErrorType}).", ex.GetType().Name);
            }

            if (!bool.TryParse(_configuration["Multitenancy:InstitutionWorker:Enabled"], out var enabled) || !enabled)
                return;
            List<ExpiryCandidate> candidates;
            using (var scan = _scopeFactory.CreateScope())
            {
                _sweepUntil ??= scan.ServiceProvider.GetRequiredService<ITimeProvider>().UtcNow;
                candidates = await scan.ServiceProvider.GetRequiredService<ISubmissionExpiryStore>().SelectAsync(_sweepUntil.Value, _cursor, _batchSize, cancellationToken);
            }

            if (candidates.Count == 0)
            {
                _cursor = null;
                _sweepUntil = null;
                return;
            }

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var item = _scopeFactory.CreateScope();
                    await item.ServiceProvider.GetRequiredService<IInstitutionExpiryService>().ProcessAsync(candidate, cancellationToken);
                }
                catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // No exception text/SQL/answers/tokens in operational logs.
                    _logger.LogError("Institution expiry failed for {InstitutionId}/{SubmissionId} ({ErrorType}).", candidate.InstitutionId, candidate.SubmissionId, ex.GetType().Name);
                }

                // Advance after failure too; retry on the next bounded sweep, no starvation.
                _cursor = candidate;
            }
        }
        finally
        {
            _cycle.Release();
        }
    }

    private async Task ProcessLegacyBatchAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("Identity:Legacy:Enabled"))
            return;
        // First scope: query expired submissions (lightweight, no answers)
        using (var listScope = _scopeFactory.CreateScope())
        {
            var submissionRepo = listScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var now = listScope.ServiceProvider.GetRequiredService<ITimeProvider>().UtcNow;
            var expired = await submissionRepo.GetExpiredInProgressAsync(now, _batchSize);
            if (expired == null || expired.Count == 0)
                return;
            foreach (var s in expired)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                // Create a separate scope per submission to avoid sharing DbContext or tracked entities
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IExamSubmissionProcessor>();
                    // Process each submission individually; exceptions are handled per-item
                    await processor.ProcessSubmitAsync(s.Id, null, SubmitReason.TimeExpired, null);
                }
                catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Expired submission failed ({ErrorType}).", ex.GetType().Name);
                // continue with next submission
                }
            }
        }
    }
}
