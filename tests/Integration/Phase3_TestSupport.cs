using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Application.DependencyInjection;
using ElectronicRegisterAPI.Business.DependencyInjection;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

internal static class Phase3
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    internal static readonly DateTime WorkerNow = new(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    internal static FixtureActor Teacher => new(SeedData.TeacherUserId, null, SeedData.TeacherId);
    internal static FixtureActor Student(ScenarioIds ids) => new(ids.StudentId, ids.StudentId, null);

    internal sealed class Clock(DateTime now) : ITimeProvider { public DateTime UtcNow => now; }
    internal sealed class InjectedFailure : Exception;

    internal static ServiceProvider Provider(IntegrationFixture fixture, ITimeProvider? clock = null, params IInterceptor[] interceptors)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var services = Services(fixture, clock, interceptors);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    internal static IServiceCollection Services(IntegrationFixture fixture, ITimeProvider? clock = null, params IInterceptor[] interceptors)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddBusinessServices();
        services.AddApplicationManagers();
        FixtureTenant.Register(services);
        services.AddDbContext<ElectronicRegisterContext>(options => options.AddInterceptors(interceptors));
        if (clock is not null)
        {
            services.RemoveAll<ITimeProvider>();
            services.AddScoped<ITimeProvider>(_ => clock);
        }
        return services;
    }

    internal static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static async Task OrderedAsync(IntegrationFixture fixture,
        Func<IServiceProvider, Task> first, Func<IServiceProvider, Task> second,
        ITimeProvider? firstClock = null, ITimeProvider? secondClock = null)
    {
        var held = Signal();
        var release = Signal();
        var save = new SaveProbe();
        save.AfterSave = async _ =>
        {
            held.TrySetResult(true);
            await release.Task.WaitAsync(Timeout);
        };
        await using var firstProvider = Provider(fixture, firstClock, save);
        await using var secondProvider = Provider(fixture, secondClock);
        using var firstScope = firstProvider.CreateScope();
        using var secondScope = secondProvider.CreateScope();
        Assert.NotSame(firstScope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>(),
            secondScope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>());
        var firstTask = SqlLockEvidence.Track(() => first(firstScope.ServiceProvider));
        Task? secondTask = null;
        try
        {
            await held.Task.WaitAsync(Timeout);
            secondTask = SqlLockEvidence.Track(() => second(secondScope.ServiceProvider));
            // Observe the actual wait-for edge, even when the canonical IAM mutex
            // now blocks the second transaction before its resource SELECT.
            var blocker = (MySqlConnector.MySqlConnection)firstScope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Database.GetDbConnection();
            await SqlLockEvidence.WaitAsync(fixture.ConnectionString, secondTask, firstTask);
        }
        finally
        {
            release.TrySetResult(true);
            await Task.WhenAll(firstTask, secondTask ?? Task.CompletedTask).WaitAsync(Timeout);
        }
    }

    internal sealed class SaveProbe : SaveChangesInterceptor
    {
        internal Func<ElectronicRegisterContext, Task>? AfterSave { get; set; }
        internal int Saves { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            // This callback runs after SaveChanges has sent and completed SQL, while the ambient transaction is open.
            Assert.NotNull(eventData.Context!.Database.CurrentTransaction);
            Saves++;
            if (AfterSave is not null) await AfterSave((ElectronicRegisterContext)eventData.Context);
            return result;
        }
    }

    internal sealed class LockProbe : DbCommandInterceptor
    {
        internal TaskCompletionSource<bool> Attempted { get; } = Signal();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase)) Attempted.TrySetResult(true);
            return ValueTask.FromResult(result);
        }
    }

    internal static async Task<string> SnapshotAsync(IServiceScopeFactory factory, ScenarioIds ids)
    {
        using var scope = factory.CreateScope();
        return await SnapshotAsync(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>(), ids);
    }

    internal static async Task<string> SnapshotAsync(ElectronicRegisterContext ctx, ScenarioIds ids)
    {
        // AsNoTracking projections execute SQL, including inside the faulting transaction. No tracked entity verification.
        var sub = await ctx.ExamSubmissions.AsNoTracking().Where(s => s.Id == ids.SubmissionId)
            .Select(s => new { s.Status, s.SubmittedAt, s.SubmitReason, s.LastClientRevision, s.GradeId, s.SuggestedGrade,
                s.ClosedScore, s.OpenScore, s.OpenMaxPoints, s.TotalScore, s.MaxScore, s.PassingScore, s.Passed,
                s.OverrideReason, s.FinalizedAt, s.ReviewedByUserId, s.UpdatedAt }).SingleAsync();
        var answers = await ctx.SubmissionAnswers.AsNoTracking().Where(a => a.SubmissionId == ids.SubmissionId)
            .OrderBy(a => a.Id).Select(a => new { a.Id, a.QuestionId, a.OpenText, a.AwardedPoints, a.TeacherComment, a.IsCorrect,
                a.LastSavedAt, Options = a.Options.OrderBy(o => o.Id).Select(o => o.Id).ToList() }).ToListAsync();
        var grades = await ctx.Grades.AsNoTracking().Where(g => g.StudentId == ids.StudentId).OrderBy(g => g.Id)
            .Select(g => new { g.Id, g.Value, g.StudentId, g.ClassSubjectId, g.Date }).ToListAsync();
        var audits = await ctx.AuditEvents.AsNoTracking().Where(a => a.EntityId == ids.SubmissionId).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Action, a.ActorUserId, a.BeforeJson, a.AfterJson, a.CreatedAt }).ToListAsync();
        return JsonSerializer.Serialize(new { Submission = sub, Answers = answers, Grades = grades, Audits = audits });
    }

    internal static async Task<SubmitExamDto> SubmitDtoAsync(IServiceScopeFactory factory, ScenarioIds ids, string? text = null)
    {
        using var scope = factory.CreateScope();
        var sub = await scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>().GetByIdAsync(ids.SubmissionId, true);
        var dto = new SubmitExamDto { ClientRevision = sub!.Revision + 1 };
        foreach (var answer in sub.Answers)
        {
            var input = new AnswerInputDto { QuestionId = answer.QuestionId, OpenText = answer.OpenText is null ? null : text ?? answer.OpenText };
            foreach (var option in answer.SelectedOptionIds) input.SelectedOptionIds.Add(option);
            dto.Answers.Add(input);
        }
        return dto;
    }

    internal static async Task<ReviewOpenAnswersDto> ReviewDtoAsync(IServiceScopeFactory factory, ScenarioIds ids, decimal points, string comment)
    {
        using var scope = factory.CreateScope();
        var review = await scope.ServiceProvider.GetRequiredService<FixtureSubmissionClient>().GetByIdAsync(ids.SubmissionId, Teacher);
        return new ReviewOpenAnswersDto { Answers = review.Answers.Where(a => a.QuestionType == QuestionType.Open)
            .Select(a => new OpenAnswerReviewInputDto { AnswerId = a.AnswerId, AwardedPoints = points, Comment = comment }).ToList() };
    }

    internal static async Task ReviewAsync(IServiceScopeFactory factory, ScenarioIds ids, decimal points = 2m)
    {
        var dto = await ReviewDtoAsync(factory, ids, points, "Committed review");
        using var scope = factory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FixtureSubmissionClient>().ReviewAsync(ids.SubmissionId, dto, Teacher);
    }
}
