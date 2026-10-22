using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.BackgroundServices;
using ElectronicRegisterAPI.Api.DependencyInjection;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class Phase3_WorkerRecoveryTests(IntegrationFixture fixture)
{
    private static IConfiguration WorkerConfiguration => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Identity:Legacy:Enabled"] = "true" }).Build();

    private static readonly IntegrationScenario[] Scenarios =
    {
        IntegrationScenario.Phase3WorkerFailure, IntegrationScenario.Phase3WorkerClosed, IntegrationScenario.Phase3WorkerOpen
    };

    private async Task<ScenarioIds[]> PrepareAsync()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var ids = new List<ScenarioIds>();
        foreach (var scenario in Scenarios) ids.Add(await fixture.ResetScenarioAsync(scenario));
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        for (var i = 0; i < ids.Count; i++)
        {
            var sub = await ctx.ExamSubmissions.FindAsync(ids[i].SubmissionId);
            sub!.DeadlineAt = Phase3.WorkerNow.AddDays(-3 + i);
            sub.StartedAt = sub.DeadlineAt.AddHours(-1);
        }
        await ctx.SaveChangesAsync();
        return ids.ToArray();
    }

    private sealed class WorkerLog : ILogger<ExpiredExamSubmissionWorker>
    {
        internal ConcurrentQueue<string> Errors { get; } = new();
        internal ConcurrentQueue<Exception> CapturedExceptions { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (level >= LogLevel.Error) Errors.Enqueue(formatter(state, exception));
            if (exception is not null) CapturedExceptions.Enqueue(exception);
        }
    }

    private sealed class DispatchLog
    {
        internal ConcurrentQueue<(Guid Id, Guid ContextId)> Calls { get; } = new();
    }

    private sealed class ObservedProcessor(IExamSubmissionProcessor inner, ElectronicRegisterContext context, DispatchLog log) : IExamSubmissionProcessor
    {
        public Task<SubmissionResultDto> ProcessSubmitAsync(Guid id, SubmitExamDto? dto, SubmitReason reason, ClaimsContext? caller)
        {
            log.Calls.Enqueue((id, context.ContextId.InstanceId));
            return inner.ProcessSubmitAsync(id, dto, reason, caller);
        }
    }

    private ServiceProvider Provider(DispatchLog log, params IInterceptor[] interceptors)
    {
        var services = Phase3.Services(fixture, new Phase3.Clock(Phase3.WorkerNow), interceptors);
        services.AddScoped<IExamSubmissionProcessor>(sp => new ObservedProcessor(
            ActivatorUtilities.CreateInstance<ExamSubmissionProcessor>(sp), sp.GetRequiredService<ElectronicRegisterContext>(), log));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class SelectionBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> _bothSelected = Phase3.Signal();
        internal int Selections;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(command.CommandText, @"`deadline_at`\s*<=") &&
                command.CommandText.Contains("ORDER BY") && command.CommandText.Contains("LIMIT"))
            {
                // Both real SELECTs have executed before either batch is dispatched.
                if (Interlocked.Increment(ref Selections) == 2) _bothSelected.TrySetResult(true);
                await _bothSelected.Task.WaitAsync(Phase3.Timeout);
            }
            return result;
        }
    }

    private async Task AssertDeliveredAsync(ScenarioIds[] ids)
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        foreach (var id in ids)
        {
            var sub = await repository.GetByIdAsync(id.SubmissionId, true);
            Assert.Equal(Phase3.WorkerNow, sub!.SubmittedAt);
            Assert.Equal(SubmitReason.TimeExpired, sub.SubmitReason);
            Assert.Equal(0, sub.Revision);
            Assert.Equal(2, sub.Answers.Count);
            Assert.Contains(id.CorrectOptionId, sub.Answers.Single(a => a.QuestionId == id.SingleQuestionId).SelectedOptionIds);
            var isOpen = id.SubmissionId == ScenarioIds.For(IntegrationScenario.Phase3WorkerOpen).SubmissionId;
            Assert.Equal(isOpen ? SubmissionStatus.PendingReview : SubmissionStatus.Graded, sub.Status);
            if (isOpen) Assert.Equal(SeedData.InitialOpenText, sub.Answers.Single(a => a.QuestionId == id.OpenQuestionId).OpenText);
            var grades = await ctx.Grades.AsNoTracking().Where(g => g.StudentId == id.StudentId).ToListAsync();
            Assert.Equal(isOpen ? 0 : 1, grades.Count);
            if (!isOpen)
            {
                Assert.Equal(sub.GradeId, grades.Single().Id);
                Assert.Equal(6m, grades.Single().Value);
            }
            var audit = Assert.Single(await ctx.AuditEvents.AsNoTracking().Where(a => a.EntityId == id.SubmissionId && a.Action == "submission.submit").ToListAsync());
            Assert.Null(audit.ActorUserId);
        }
    }

    [Fact]
    public async Task HostedWorker_ResolvesWithScopeValidation()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var services = Phase3.Services(fixture);
        services.AddApiServices();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.NotEmpty(provider.GetServices<IHostedService>());
    }

    [Fact]
    public async Task ControlledStopBeforeDispatch_NewWorkerRecoversPersistedExpiredBatch()
    {
        var ids = await PrepareAsync();
        var before = new List<string>();
        foreach (var id in ids) before.Add(await Phase3.SnapshotAsync(fixture.ScopeFactory, id));
        var stoppedCalls = new DispatchLog();
        var stoppedLog = new WorkerLog();
        await using (var provider = Provider(stoppedCalls))
        using (var first = new ExpiredExamSubmissionWorker(provider.GetRequiredService<IServiceScopeFactory>(), stoppedLog, WorkerConfiguration))
        {
            await first.StartAsync(CancellationToken.None);
            await first.StopAsync(CancellationToken.None);
        }
        Assert.Empty(stoppedCalls.Calls);
        Assert.Empty(stoppedLog.Errors);
        for (var i = 0; i < ids.Length; i++) Assert.Equal(before[i], await Phase3.SnapshotAsync(fixture.ScopeFactory, ids[i]));

        var calls = new DispatchLog();
        var log = new WorkerLog();
        await using var restartedProvider = Provider(calls);
        using var restarted = new ExpiredExamSubmissionWorker(restartedProvider.GetRequiredService<IServiceScopeFactory>(), log, WorkerConfiguration);
        // Drive one real batch on the new instance; no wait for the 15-second production timer.
        await restarted.ProcessBatchAsync(CancellationToken.None).WaitAsync(Phase3.Timeout);
        await AssertDeliveredAsync(ids);
        Assert.Equal(ids.Length, calls.Calls.Count);
        Assert.Equal(ids.Length, calls.Calls.Select(c => c.ContextId).Distinct().Count());
        var completed = new List<string>();
        foreach (var id in ids) completed.Add(await Phase3.SnapshotAsync(fixture.ScopeFactory, id));
        await restarted.ProcessBatchAsync(CancellationToken.None).WaitAsync(Phase3.Timeout);
        Assert.Equal(ids.Length, calls.Calls.Count); // completed submissions are not selected again
        for (var i = 0; i < ids.Length; i++) Assert.Equal(completed[i], await Phase3.SnapshotAsync(fixture.ScopeFactory, ids[i]));
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task TwoWorkers_SelectSameBatch_CommitOneDeliveryEachWithoutTechnicalFailures()
    {
        var ids = await PrepareAsync();
        var selections = new SelectionBarrier();
        var firstCalls = new DispatchLog();
        var secondCalls = new DispatchLog();
        var firstLog = new WorkerLog();
        var secondLog = new WorkerLog();
        var firstWrites = new Phase3.SaveProbe();
        var secondWrites = new Phase3.SaveProbe();
        await using var p1 = Provider(firstCalls, selections, firstWrites);
        await using var p2 = Provider(secondCalls, selections, secondWrites);
        using var w1 = new ExpiredExamSubmissionWorker(p1.GetRequiredService<IServiceScopeFactory>(), firstLog, WorkerConfiguration);
        using var w2 = new ExpiredExamSubmissionWorker(p2.GetRequiredService<IServiceScopeFactory>(), secondLog, WorkerConfiguration);
        await Task.WhenAll(w1.ProcessBatchAsync(CancellationToken.None), w2.ProcessBatchAsync(CancellationToken.None)).WaitAsync(Phase3.Timeout);
        Assert.Equal(2, selections.Selections);
        foreach (var id in ids)
        {
            Assert.Single(firstCalls.Calls.Where(c => c.Id == id.SubmissionId));
            Assert.Single(secondCalls.Calls.Where(c => c.Id == id.SubmissionId));
        }
        Assert.Equal(ids.Length * 2, firstCalls.Calls.Concat(secondCalls.Calls).Select(c => c.ContextId).Distinct().Count());
        Assert.Empty(firstLog.Errors);
        Assert.Empty(secondLog.Errors);
        // Two closed-only deliveries have four saves each, the pending review delivery has three.
        // The three losing dispatches must execute no SaveChanges at all.
        Assert.Equal(11, firstWrites.Saves + secondWrites.Saves);
        await AssertDeliveredAsync(ids);
    }

    [Fact]
    public async Task ItemFailureAfterSql_DoesNotStopBatch_AndNextBatchRecoversWithFreshScope()
    {
        var ids = await PrepareAsync();
        var failed = ids[0]; // oldest, so the successful items must be dispatched after the failure
        var before = await Phase3.SnapshotAsync(fixture.ScopeFactory, failed);
        string? writtenBeforeFailure = null;
        var injected = 0;
        var probe = new Phase3.SaveProbe { AfterSave = async ctx =>
        {
            if (!ctx.AuditEvents.Local.Any(a => a.EntityId == failed.SubmissionId && a.Action == "submission.submit")) return;
            if (Interlocked.Exchange(ref injected, 1) != 0) return;
            writtenBeforeFailure = await Phase3.SnapshotAsync(ctx, failed);
            throw new Phase3.InjectedFailure();
        }};
        var firstCalls = new DispatchLog();
        var firstLog = new WorkerLog();
        await using (var provider = Provider(firstCalls, probe))
        using (var worker = new ExpiredExamSubmissionWorker(provider.GetRequiredService<IServiceScopeFactory>(), firstLog, WorkerConfiguration))
            await worker.ProcessBatchAsync(CancellationToken.None).WaitAsync(Phase3.Timeout);
        Assert.Equal(1, injected);
        Assert.NotNull(writtenBeforeFailure);
        Assert.NotEqual(before, writtenBeforeFailure);
        Assert.Equal(before, await Phase3.SnapshotAsync(fixture.ScopeFactory, failed));
        Assert.Contains(nameof(Phase3.InjectedFailure), Assert.Single(firstLog.Errors));
        Assert.Empty(firstLog.CapturedExceptions); // Failure type retained; SQL/exception text cannot reach logs.
        Assert.Equal(ids.Length, firstCalls.Calls.Count);
        Assert.Equal(failed.SubmissionId, firstCalls.Calls.First().Id);
        await AssertDeliveredAsync(ids.Skip(1).ToArray());
        var recoveredCalls = new DispatchLog();
        var recoveredLog = new WorkerLog();
        await using (var provider = Provider(recoveredCalls))
        using (var worker = new ExpiredExamSubmissionWorker(provider.GetRequiredService<IServiceScopeFactory>(), recoveredLog, WorkerConfiguration))
            await worker.ProcessBatchAsync(CancellationToken.None).WaitAsync(Phase3.Timeout);
        Assert.Equal(failed.SubmissionId, Assert.Single(recoveredCalls.Calls).Id);
        Assert.DoesNotContain(recoveredCalls.Calls.Single().ContextId, firstCalls.Calls.Select(c => c.ContextId));
        Assert.Empty(recoveredLog.Errors);
        await AssertDeliveredAsync(ids);
    }
}
