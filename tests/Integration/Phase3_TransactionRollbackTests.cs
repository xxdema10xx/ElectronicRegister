using System;
using System.Linq;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class Phase3_TransactionRollbackTests(IntegrationFixture fixture)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ManualSubmit_RollsBackReplacementAndCompletedWrites(bool closedOnly, bool failAfterAudit)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var ids = await fixture.ResetScenarioAsync(closedOnly ? IntegrationScenario.ClosedOnly : IntegrationScenario.ExamRegression);
        var before = await Phase3.SnapshotAsync(fixture.ScopeFactory, ids);
        var dto = await Phase3.SubmitDtoAsync(fixture.ScopeFactory, ids, "Replacement that must roll back");
        // Remove one persisted answer, including its option links, as well as replacing the other answer.
        dto.Answers.RemoveAt(closedOnly ? 0 : 1);
        var probe = new Phase3.SaveProbe();
        string? inTransaction = null;
        probe.AfterSave = async ctx =>
        {
            if (failAfterAudit && !ctx.AuditEvents.Local.Any(a => a.EntityId == ids.SubmissionId && a.Action == "submission.submit")) return;
            inTransaction = await Phase3.SnapshotAsync(ctx, ids);
            throw new Phase3.InjectedFailure();
        };
        await using (var provider = Phase3.Provider(fixture, null, probe))
        using (var scope = provider.CreateScope())
        {
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => scope.ServiceProvider.GetRequiredService<FixtureExamClient>()
                .SubmitAsync(ids.SubmissionId, dto, Phase3.Student(ids)));
        }
        Assert.True(probe.Saves > 0);
        Assert.NotNull(inTransaction);
        Assert.NotEqual(before, inTransaction);
        using (var written = System.Text.Json.JsonDocument.Parse(inTransaction!))
        {
            Assert.Single(written.RootElement.GetProperty("Answers").EnumerateArray());
            Assert.Equal(dto.ClientRevision, written.RootElement.GetProperty("Submission").GetProperty("LastClientRevision").GetInt64());
            if (failAfterAudit)
            {
                Assert.Single(written.RootElement.GetProperty("Audits").EnumerateArray());
                Assert.Equal(closedOnly ? 1 : 0, written.RootElement.GetProperty("Grades").GetArrayLength());
            }
        }
        Assert.Equal(before, await Phase3.SnapshotAsync(fixture.ScopeFactory, ids));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticSubmit_RollsBackGradeSubmissionAndAudit(bool closedOnly)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var ids = await fixture.ResetScenarioAsync(closedOnly ? IntegrationScenario.ClosedOnly : IntegrationScenario.ExamRegression);
        var before = await Phase3.SnapshotAsync(fixture.ScopeFactory, ids);
        var probe = new Phase3.SaveProbe();
        string? inTransaction = null;
        probe.AfterSave = async ctx =>
        {
            if (!ctx.AuditEvents.Local.Any(a => a.EntityId == ids.SubmissionId && a.Action == "submission.submit")) return;
            inTransaction = await Phase3.SnapshotAsync(ctx, ids);
            throw new Phase3.InjectedFailure();
        };
        // A fixed future clock makes the seeded deadline deterministically expired, without changing the snapshot.
        await using (var provider = Phase3.Provider(fixture, new Phase3.Clock(DateTime.UtcNow.AddDays(1)), probe))
        using (var scope = provider.CreateScope())
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => scope.ServiceProvider.GetRequiredService<FixtureExpiryClient>().ProcessAsync(ids.SubmissionId));
        Assert.NotNull(inTransaction);
        Assert.NotEqual(before, inTransaction);
        using (var written = System.Text.Json.JsonDocument.Parse(inTransaction!))
        {
            Assert.Single(written.RootElement.GetProperty("Audits").EnumerateArray());
            Assert.Equal(closedOnly ? 1 : 0, written.RootElement.GetProperty("Grades").GetArrayLength());
            Assert.Equal("time_expired", written.RootElement.GetProperty("Submission").GetProperty("SubmitReason").GetString());
        }
        Assert.Equal(before, await Phase3.SnapshotAsync(fixture.ScopeFactory, ids));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Finalize_RollsBackNewOrUpdatedGradeAndAudit(bool isOverride, bool existingGrade)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        ScenarioIds ids;
        if (existingGrade)
        {
            ids = await fixture.ResetScenarioAsync(IntegrationScenario.ClosedOnly);
            var submit = await Phase3.SubmitDtoAsync(fixture.ScopeFactory, ids);
            using var scope = fixture.ScopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<FixtureExamClient>().SubmitAsync(ids.SubmissionId, submit, Phase3.Student(ids));
        }
        else
        {
            ids = await fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
            await Phase3.ReviewAsync(fixture.ScopeFactory, ids);
        }
        var before = await Phase3.SnapshotAsync(fixture.ScopeFactory, ids);
        string? inTransaction = null;
        var probe = new Phase3.SaveProbe
        {
            AfterSave = async ctx =>
            {
                if (!ctx.AuditEvents.Local.Any(a => a.EntityId == ids.SubmissionId && a.Action is "submission.finalize" or "submission.override")) return;
                inTransaction = await Phase3.SnapshotAsync(ctx, ids);
                throw new Phase3.InjectedFailure();
            }
        };
        await using (var provider = Phase3.Provider(fixture, null, probe))
        using (var scope = provider.CreateScope())
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => scope.ServiceProvider.GetRequiredService<FixtureSubmissionClient>()
                .FinalizeAsync(ids.SubmissionId, isOverride ? new FinalizeSubmissionDto { FinalGrade = 8m, OverrideReason = "Rollback override" } : new(), Phase3.Teacher));
        Assert.NotNull(inTransaction);
        using (var written = System.Text.Json.JsonDocument.Parse(inTransaction!))
        {
            Assert.Single(written.RootElement.GetProperty("Grades").EnumerateArray());
            Assert.Equal("finalized", written.RootElement.GetProperty("Submission").GetProperty("Status").GetString());
            Assert.Equal(isOverride ? 8m : 7m, written.RootElement.GetProperty("Grades")[0].GetProperty("Value").GetDecimal());
        }
        Assert.Equal(before, await Phase3.SnapshotAsync(fixture.ScopeFactory, ids));
    }

    [Fact]
    public async Task Review_RollsBackActuallyWrittenPointsCommentAndSuggestedGrade()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var ids = await fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        await Phase3.ReviewAsync(fixture.ScopeFactory, ids);
        var dto = await Phase3.ReviewDtoAsync(fixture.ScopeFactory, ids, 5m, "Must not persist");
        var before = await Phase3.SnapshotAsync(fixture.ScopeFactory, ids);
        string? inTransaction = null;
        var probe = new Phase3.SaveProbe { AfterSave = async ctx =>
        {
            inTransaction = await Phase3.SnapshotAsync(ctx, ids);
            throw new Phase3.InjectedFailure();
        }};
        await using (var provider = Phase3.Provider(fixture, null, probe))
        using (var scope = provider.CreateScope())
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => scope.ServiceProvider.GetRequiredService<FixtureSubmissionClient>()
                .ReviewAsync(ids.SubmissionId, dto, Phase3.Teacher));
        Assert.Equal(1, probe.Saves);
        Assert.NotEqual(before, inTransaction);
        using (var written = System.Text.Json.JsonDocument.Parse(inTransaction!))
        {
            Assert.Equal(10m, written.RootElement.GetProperty("Submission").GetProperty("SuggestedGrade").GetDecimal());
            var answer = written.RootElement.GetProperty("Answers").EnumerateArray()
                .Single(a => a.GetProperty("QuestionId").GetGuid() == ids.OpenQuestionId);
            Assert.Equal(5m, answer.GetProperty("AwardedPoints").GetDecimal());
            Assert.Equal("Must not persist", answer.GetProperty("TeacherComment").GetString());
        }
        Assert.Equal(before, await Phase3.SnapshotAsync(fixture.ScopeFactory, ids));
    }
}
