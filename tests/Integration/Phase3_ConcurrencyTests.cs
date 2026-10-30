using System;
using System.Linq;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class Phase3_ConcurrencyTests(IntegrationFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReviewAndFinalize_BothLockOrdersUseOnlyCommittedCompleteReview(bool finalizeFirst)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var ids = await fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        // Complete the initial review legitimately: Finalize is never allowed to bypass review prerequisites.
        await Phase3.ReviewAsync(fixture.ScopeFactory, ids, 2m);
        var changedReview = await Phase3.ReviewDtoAsync(fixture.ScopeFactory, ids, 5m, "Latest committed review");
        string? finalizedSnapshot = null;
        async Task FinalizeAsync(IServiceProvider sp)
        {
            await sp.GetRequiredService<FixtureSubmissionClient>().FinalizeAsync(ids.SubmissionId, new(), Phase3.Teacher);
            finalizedSnapshot = await Phase3.SnapshotAsync(fixture.ScopeFactory, ids);
        }
        Task ReviewAsync(IServiceProvider sp) => sp.GetRequiredService<FixtureSubmissionClient>()
            .ReviewAsync(ids.SubmissionId, changedReview, Phase3.Teacher);
        if (finalizeFirst)
            await Phase3.OrderedAsync(fixture, FinalizeAsync, async sp =>
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReviewAsync(sp));
                Assert.True(SubmissionErrors.IsConflict(error));
            });
        else
            await Phase3.OrderedAsync(fixture, ReviewAsync, FinalizeAsync);

        using var verify = fixture.ScopeFactory.CreateScope();
        var repo = verify.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
        var sub = await repo.GetByIdAsync(ids.SubmissionId, true);
        Assert.Equal(SubmissionStatus.Finalized, sub!.Status);
        Assert.Equal(finalizeFirst ? 2m : 5m, sub.Answers.Single(a => a.QuestionId == ids.OpenQuestionId).AwardedPoints);
        Assert.Equal(finalizeFirst ? 7m : 10m, sub.SuggestedGrade);
        Assert.True(sub.Passed);
        var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var grade = Assert.Single(await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync());
        Assert.Equal(sub.SuggestedGrade, grade.Value);
        Assert.Equal(sub.GradeId, grade.Id);
        Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == ids.SubmissionId && a.Action == "submission.finalize").ToListAsync());
        Assert.Equal(finalizedSnapshot, await Phase3.SnapshotAsync(fixture.ScopeFactory, ids));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ManualAndAutomaticSubmit_BothLockOrdersDeliverExactlyOnce(bool automaticFirst, bool closedOnly)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var ids = await fixture.ResetScenarioAsync(closedOnly ? IntegrationScenario.ClosedOnly : IntegrationScenario.ExamRegression);
        DateTime deadline;
        using (var scope = fixture.ScopeFactory.CreateScope())
            deadline = (await scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>().GetByIdAsync(ids.SubmissionId))!.DeadlineAt;
        var manualClock = new Phase3.Clock(automaticFirst ? deadline.AddSeconds(1) : deadline.AddSeconds(-1));
        var autoClock = new Phase3.Clock(deadline.AddSeconds(1));
        var dto = await Phase3.SubmitDtoAsync(fixture.ScopeFactory, ids, "Last valid submitted text");
        if (closedOnly) dto.Answers.RemoveAt(0);
        Task Manual(IServiceProvider sp) => sp.GetRequiredService<FixtureExamClient>().SubmitAsync(ids.SubmissionId, dto, Phase3.Student(ids));
        Task Auto(IServiceProvider sp) => sp.GetRequiredService<FixtureExpiryClient>().ProcessAsync(ids.SubmissionId);
        await Phase3.OrderedAsync(fixture, automaticFirst ? Auto : Manual, automaticFirst ? Manual : Auto,
            automaticFirst ? autoClock : manualClock, automaticFirst ? manualClock : autoClock);

        using var verify = fixture.ScopeFactory.CreateScope();
        var sub = await verify.ServiceProvider.GetRequiredService<IExamSubmissionRepository>().GetByIdAsync(ids.SubmissionId, true);
        Assert.Equal(automaticFirst ? SubmitReason.TimeExpired : SubmitReason.Manual, sub!.SubmitReason);
        Assert.Equal(automaticFirst ? autoClock.UtcNow : manualClock.UtcNow, sub.SubmittedAt);
        Assert.Equal(automaticFirst ? 0 : dto.ClientRevision, sub.Revision);
        if (!closedOnly)
            Assert.Equal(automaticFirst ? SeedData.InitialOpenText : "Last valid submitted text", sub.Answers.Single(a => a.QuestionId == ids.OpenQuestionId).OpenText);
        else
            Assert.Equal(automaticFirst ? 2 : 1, sub.Answers.Count);
        Assert.Contains(ids.CorrectOptionId, sub.Answers.Single(a => a.QuestionId == ids.SingleQuestionId).SelectedOptionIds);
        Assert.Equal(closedOnly ? SubmissionStatus.Graded : SubmissionStatus.PendingReview, sub.Status);
        var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var audit = Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == ids.SubmissionId && a.Action == "submission.submit").ToListAsync());
        Assert.Equal(automaticFirst ? (Guid?)null : ids.StudentId, audit.ActorUserId);
        var grades = await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync();
        Assert.Equal(closedOnly ? 1 : 0, grades.Count);
        if (closedOnly) Assert.Equal(sub.GradeId, grades.Single().Id);
        var committed = await Phase3.SnapshotAsync(fixture.ScopeFactory, ids);
        // Retry of the automatic delivery after either winner performs no further writes.
        using (var retry = fixture.ScopeFactory.CreateScope())
            await retry.ServiceProvider.GetRequiredService<FixtureExpiryClient>().ProcessAsync(ids.SubmissionId);
        Assert.Equal(committed, await Phase3.SnapshotAsync(fixture.ScopeFactory, ids));
    }

    [Fact]
    public async Task TimingGuards_RejectEarlyAutomaticAndLateManualWithoutWrites()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var ids = await fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);
        var before = await Phase3.SnapshotAsync(fixture.ScopeFactory, ids);
        var dto = await Phase3.SubmitDtoAsync(fixture.ScopeFactory, ids, "Too late");
        await using (var early = Phase3.Provider(fixture, new Phase3.Clock(DateTime.UtcNow.AddDays(-1))))
        using (var scope = early.CreateScope())
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<FixtureExpiryClient>().ProcessAsync(ids.SubmissionId));
            Assert.Contains("Non è ancora tempo", error.Message);
        }
        await using (var late = Phase3.Provider(fixture, new Phase3.Clock(DateTime.UtcNow.AddDays(1))))
        using (var scope = late.CreateScope())
            await Assert.ThrowsAsync<BusinessRuleException>(() => scope.ServiceProvider.GetRequiredService<FixtureExamClient>().SubmitAsync(ids.SubmissionId, dto, Phase3.Student(ids)));
        Assert.Equal(before, await Phase3.SnapshotAsync(fixture.ScopeFactory, ids));
    }
}
