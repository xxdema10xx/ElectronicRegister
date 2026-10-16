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

namespace ElectronicRegisterAPI.Api.BackgroundServices
{
    internal class ExpiredExamSubmissionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiredExamSubmissionWorker> _logger;
        private readonly ITimeProvider _timeProvider;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(15);
        private readonly int _batchSize = 50;

        public ExpiredExamSubmissionWorker(IServiceScopeFactory scopeFactory, ILogger<ExpiredExamSubmissionWorker> logger, ITimeProvider timeProvider)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _timeProvider = timeProvider;
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
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing expired submissions batch.");
                    }
                }
            }
            finally
            {
                _logger.LogInformation("ExpiredExamSubmissionWorker stopping.");
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            // First scope: query expired submissions (lightweight, no answers)
            using (var listScope = _scopeFactory.CreateScope())
            {
                var submissionRepo = listScope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();

                var now = _timeProvider.UtcNow;
                var expired = await submissionRepo.GetExpiredInProgressAsync(now, _batchSize);

                if (expired == null || expired.Count == 0) return;

                foreach (var s in expired)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Create a separate scope per submission to avoid sharing DbContext or tracked entities
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var processor = scope.ServiceProvider.GetRequiredService<IExamSubmissionProcessor>();

                        // Process each submission individually; exceptions are handled per-item
                        await processor.ProcessSubmitAsync(s.Id, null, SubmitReason.TimeExpired, null);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process expired submission {SubmissionId}", s.Id);
                        // continue with next submission
                    }
                }
            }
        }
    }
}
