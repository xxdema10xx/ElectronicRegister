using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class Phase2C_VisibilityTests
{
    private readonly IntegrationFixture _fixture;
    public Phase2C_VisibilityTests(IntegrationFixture fixture) => _fixture = fixture;
    private static ClaimsContext Teacher => new(SeedData.TeacherUserId, UserRole.Teacher, null, SeedData.TeacherId);
    private static ClaimsContext Student(ScenarioIds ids) => new(ids.StudentId, UserRole.Student, ids.StudentId, null);

    [Theory]
    [InlineData(SubmissionStatus.InProgress)]
    [InlineData(SubmissionStatus.PendingReview)]
    [InlineData(SubmissionStatus.FailedAuto)]
    [InlineData(SubmissionStatus.Graded)]
    [InlineData(SubmissionStatus.Finalized)]
    public async Task UnpublishedStates_NeverExposeProvisionalValues(SubmissionStatus status)
    {
        Assert.True(_fixture.IsAvailable, _fixture.UnavailableReason);
        var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);
        using (var scope = _fixture.ScopeFactory.CreateScope())
        {
            // Deliberately populate provisional fields to test redaction, not the grading calculation.
            var repository = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            var submission = await repository.GetByIdAsync(ids.SubmissionId, true);
            submission!.Status = status;
            submission.SuggestedGrade = 9m;
            submission.TotalScore = 9m;
            submission.Passed = true;
            await repository.UpdateAfterGradingAsync(submission);
            var mgr = scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
            var staff = await mgr.GetByIdAsync(ids.SubmissionId, Teacher);
            Assert.Equal(9m, staff.SuggestedGrade);
            var student = await mgr.GetByIdAsync(ids.SubmissionId, Student(ids));
            Assert.Null(student.SuggestedGrade);
            Assert.Null(student.TotalScore);
            Assert.Null(student.Passed);
        }
        await using var host = await Phase2C_HttpHost.StartAsync(_fixture.ConnectionString);
        var result = await host.As(Student(ids)).GetFromJsonAsync<SubmissionResultDto>($"/api/Exam/submission/{ids.SubmissionId}/result");
        Assert.Equal(status, result!.Status);
        Assert.Null(result.TotalScore);
        Assert.Null(result.Grade);
        Assert.Null(result.Passed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StudentOwnWork_RedactsInProgressAndReviewedPending(bool reviewed)
    {
        Assert.True(_fixture.IsAvailable, _fixture.UnavailableReason);
        var ids = reviewed
            ? await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression)
            : await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);
        using var scope = _fixture.ScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
        if (reviewed)
        {
            var initial = await manager.GetByIdAsync(ids.SubmissionId, Teacher);
            await manager.ReviewAsync(ids.SubmissionId, new ReviewOpenAnswersDto
            {
                Answers = initial.Answers.Where(a => a.QuestionType == QuestionType.Open)
                    .Select(a => new OpenAnswerReviewInputDto { AnswerId = a.AnswerId, AwardedPoints = 5m, Comment = "Private teacher comment" }).ToList()
            }, Teacher);
            var staff = await manager.GetByIdAsync(ids.SubmissionId, Teacher);
            Assert.Equal(10m, staff.SuggestedGrade);
            Assert.True(staff.Passed);
        }
        var own = await manager.GetByIdAsync(ids.SubmissionId, Student(ids));
        Assert.NotEmpty(own.Answers);
        Assert.Contains(own.Answers, a => a.OpenText != null);
        Assert.Null(own.SuggestedGrade);
        Assert.Null(own.FinalGrade);
        Assert.Null(own.TotalScore);
        Assert.Null(own.ClosedScore);
        Assert.Null(own.OpenScore);
        Assert.Null(own.Passed);
        Assert.Null(own.ReviewedByUserId);
        Assert.All(own.Answers, a =>
        {
            Assert.Null(a.AwardedPoints);
            Assert.Null(a.IsCorrect);
            Assert.Null(a.TeacherComment);
            Assert.All(a.Options, o => Assert.False(o.IsCorrect));
        });
        await using var host = await Phase2C_HttpHost.StartAsync(_fixture.ConnectionString);
        var result = await host.As(Student(ids)).GetFromJsonAsync<SubmissionResultDto>($"/api/Exam/submission/{ids.SubmissionId}/result");
        Assert.Null(result!.Grade);
        Assert.Null(result.TotalScore);
        Assert.Null(result.MaxScore);
        Assert.Null(result.Passed);
    }

    [Fact]
    public async Task Student_CannotReadOthersOrInvokeReviewManagers()
    {
        Assert.True(_fixture.IsAvailable, _fixture.UnavailableReason);
        var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        var other = new ClaimsContext(Guid.NewGuid(), UserRole.Student, Guid.NewGuid(), null);
        using var scope = _fixture.ScopeFactory.CreateScope();
        var mgr = scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManager>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => mgr.GetByIdAsync(ids.SubmissionId, other));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => exams.GetResultAsync(ids.SubmissionId, other));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => mgr.GetByExamAsync(ids.ExamId, null, Student(ids)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => mgr.ReviewAsync(ids.SubmissionId, new ReviewOpenAnswersDto(), Student(ids)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), Student(ids)));
        await using var host = await Phase2C_HttpHost.StartAsync(_fixture.ConnectionString);
        var client = host.As(other);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/Exam/submission/{ids.SubmissionId}/result")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/Exam/{ids.ExamId}")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StudentSubmitResponse_OnlyPublishesPersistedOfficialGrade(bool closedOnly)
    {
        Assert.True(_fixture.IsAvailable, _fixture.UnavailableReason);
        var ids = await _fixture.ResetScenarioAsync(closedOnly ? IntegrationScenario.ClosedOnly : IntegrationScenario.ExamRegression);
        using var scope = _fixture.ScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
        var submission = await repository.GetByIdAsync(ids.SubmissionId, true);
        var dto = new SubmitExamDto { ClientRevision = submission!.Revision + 1 };
        foreach (var answer in submission.Answers)
        {
            var input = new AnswerInputDto { QuestionId = answer.QuestionId, OpenText = answer.OpenText };
            foreach (var option in answer.SelectedOptionIds) input.SelectedOptionIds.Add(option);
            dto.Answers.Add(input);
        }
        await using var host = await Phase2C_HttpHost.StartAsync(_fixture.ConnectionString);
        var client = host.As(Student(ids));
        var response = await client.PostAsJsonAsync($"/api/Exam/{ids.SubmissionId}/submit", dto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var submitted = await response.Content.ReadFromJsonAsync<SubmissionResultDto>();
        var retrieved = await client.GetFromJsonAsync<SubmissionResultDto>($"/api/Exam/submission/{ids.SubmissionId}/result");
        Assert.Equal(SubmitReason.Manual, submitted!.SubmitReason);
        Assert.Equal(submitted.Grade, retrieved!.Grade);
        Assert.Equal(submitted.Passed, retrieved.Passed);
        var review = await scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>().GetByIdAsync(ids.SubmissionId, Student(ids));
        Assert.Null(review.SuggestedGrade);
        Assert.All(review.Answers.SelectMany(a => a.Options), o => Assert.False(o.IsCorrect));
        if (closedOnly)
        {
            Assert.Equal(SubmissionStatus.Graded, retrieved.Status);
            var persisted = await repository.GetByIdAsync(ids.SubmissionId);
            var official = await scope.ServiceProvider.GetRequiredService<IGradeRepository>().GetByIdAsync(persisted!.GradeId!.Value);
            Assert.Equal(official!.Value, retrieved.Grade);
            Assert.Equal(official.Value, review.FinalGrade);
            Assert.True(retrieved.Passed);
        }
        else
        {
            Assert.Equal(SubmissionStatus.PendingReview, retrieved.Status);
            Assert.Null(submitted.Grade);
            Assert.Null(submitted.TotalScore);
            Assert.Null(submitted.MaxScore);
            Assert.Null(submitted.Passed);
            Assert.Null(review.FinalGrade);
        }
    }
}
