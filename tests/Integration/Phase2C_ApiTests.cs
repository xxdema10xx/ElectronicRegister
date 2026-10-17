using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class Phase2C_ApiTests : IAsyncLifetime
{
    private readonly IntegrationFixture _fixture;
    private Phase2C_HttpHost _host = null!;
    public Phase2C_ApiTests(IntegrationFixture fixture) => _fixture = fixture;
    private static ClaimsContext Teacher => new(SeedData.TeacherUserId, UserRole.Teacher, null, SeedData.TeacherId);
    private static ClaimsContext Admin => new(SeedData.AdminUserId, UserRole.Admin, null, null);
    private static ClaimsContext OtherTeacher => new(SeedData.Id(0, 13), UserRole.Teacher, null, SeedData.Id(0, 12));
    private static ClaimsContext Student(ScenarioIds ids) => new(ids.StudentId, UserRole.Student, ids.StudentId, null);
    private static string ReviewUrl(ScenarioIds ids) => $"/api/Exam/submission/{ids.SubmissionId}/review";
    private static string FinalizeUrl(ScenarioIds ids) => $"/api/Exam/submission/{ids.SubmissionId}/finalize";
    private static string ResultUrl(ScenarioIds ids) => $"/api/Exam/submission/{ids.SubmissionId}/result";

    public async Task InitializeAsync()
    {
        Assert.True(_fixture.IsAvailable, _fixture.UnavailableReason);
        _host = await Phase2C_HttpHost.StartAsync(_fixture.ConnectionString);
    }
    public async Task DisposeAsync() { if (_host is not null) await _host.DisposeAsync(); }

    private async Task<ReviewOpenAnswersDto> ReviewDtoAsync(ScenarioIds ids)
    {
        using var scope = _fixture.ScopeFactory.CreateScope();
        var review = await scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>().GetByIdAsync(ids.SubmissionId, Teacher);
        return new ReviewOpenAnswersDto
        {
            Answers = review.Answers.Where(a => a.QuestionType == QuestionType.Open)
                .Select(a => new OpenAnswerReviewInputDto { AnswerId = a.AnswerId, AwardedPoints = 2m, Comment = "Internal review comment" }).ToList()
        };
    }

    [Theory]
    [InlineData("list")]
    [InlineData("read")]
    [InlineData("review")]
    [InlineData("finalize")]
    public async Task ReviewApis_RejectStudentAndAnonymous(string operation)
    {
        var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        foreach (var caller in new ClaimsContext?[] { Student(ids), null })
        {
            var response = await SendOperationAsync(ids, operation, caller);
            Assert.Equal(caller is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private Task<HttpResponseMessage> SendOperationAsync(ScenarioIds ids, string operation, ClaimsContext? caller)
    {
        var client = _host.As(caller);
        return operation switch
        {
            "list" => client.GetAsync($"/api/Exam/{ids.ExamId}/submissions"),
            "read" => client.GetAsync(ReviewUrl(ids)),
            "review" => client.PutAsJsonAsync(ReviewUrl(ids), new ReviewOpenAnswersDto()),
            _ => client.PostAsJsonAsync(FinalizeUrl(ids), new FinalizeSubmissionDto())
        };
    }

    [Theory]
    [InlineData("list")]
    [InlineData("read")]
    [InlineData("review")]
    [InlineData("finalize")]
    public async Task NonOwnerTeacher_ForbiddenAtHttpAndApplicationLevels(string operation)
    {
        var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendOperationAsync(ids, operation, OtherTeacher)).StatusCode);
        using var scope = _fixture.ScopeFactory.CreateScope();
        var mgr = scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            switch (operation)
            {
                case "list": await mgr.GetByExamAsync(ids.ExamId, null, OtherTeacher); break;
                case "read": await mgr.GetByIdAsync(ids.SubmissionId, OtherTeacher); break;
                case "review": await mgr.ReviewAsync(ids.SubmissionId, new ReviewOpenAnswersDto(), OtherTeacher); break;
                default: await mgr.FinalizeAsync(ids.SubmissionId, new FinalizeSubmissionDto(), OtherTeacher); break;
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerAndAdmin_ReviewFinalizeRetryAndConflict(bool admin)
    {
        var caller = admin ? Admin : Teacher;
        var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        var client = _host.As(caller);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Exam/{ids.ExamId}/submissions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ReviewUrl(ids))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(ReviewUrl(ids), await ReviewDtoAsync(ids))).StatusCode);
        var final = new FinalizeSubmissionDto { FinalGrade = 8m, OverrideReason = "Official override" };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(FinalizeUrl(ids), final)).StatusCode);
        var original = await client.GetFromJsonAsync<SubmissionReviewDto>(ReviewUrl(ids));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(FinalizeUrl(ids), final)).StatusCode);
        foreach (var different in new[]
        {
            new FinalizeSubmissionDto { FinalGrade = 6m, OverrideReason = final.OverrideReason },
            new FinalizeSubmissionDto { FinalGrade = 8m, OverrideReason = "Different reason" }
        })
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(FinalizeUrl(ids), different)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(ReviewUrl(ids), await ReviewDtoAsync(ids))).StatusCode);
        var review = await client.GetFromJsonAsync<SubmissionReviewDto>(ReviewUrl(ids));
        Assert.Equal(8m, review!.FinalGrade);
        Assert.Equal(original!.FinalizedAt, review.FinalizedAt);
        Assert.Equal(original.ReviewedByUserId, review.ReviewedByUserId);
        Assert.Equal(7m, review.SuggestedGrade);
        Assert.True(review.Passed);
        Assert.All(review.Answers.Where(a => a.QuestionType == QuestionType.Open), a => Assert.Equal("Internal review comment", a.TeacherComment));

        var result = await _host.As(Student(ids)).GetFromJsonAsync<SubmissionResultDto>(ResultUrl(ids));
        Assert.Equal(8m, result!.Grade);
        Assert.True(result.Passed);
        using var scope = _fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal(8m, Assert.Single(await ctx.Grades.Where(g => g.StudentId == ids.StudentId).ToListAsync()).Value);
        var audit = Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == ids.SubmissionId && a.Action == "submission.override").ToListAsync());
        Assert.Equal(caller.UserId, audit.ActorUserId);
        Assert.NotNull(audit.BeforeJson);
        Assert.NotNull(audit.AfterJson);
        Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == ids.SubmissionId &&
            (a.Action == "submission.override" || a.Action == "submission.finalize")).ToListAsync());
        var ownReview = await scope.ServiceProvider.GetRequiredService<IExamSubmissionManager>().GetByIdAsync(ids.SubmissionId, Student(ids));
        Assert.Equal(8m, ownReview.FinalGrade);
        Assert.Null(ownReview.SuggestedGrade);
        Assert.All(ownReview.Answers, a =>
        {
            Assert.Null(a.IsCorrect);
            Assert.Null(a.AwardedPoints);
            Assert.Null(a.TeacherComment);
            Assert.All(a.Options, o => Assert.False(o.IsCorrect));
        });
    }

    [Fact]
    public async Task FinalizeWithoutOverride_UsesSuggestedAndOrdinaryAudit()
    {
        var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        var client = _host.As(Teacher);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(ReviewUrl(ids), await ReviewDtoAsync(ids))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(FinalizeUrl(ids), new FinalizeSubmissionDto())).StatusCode);
        var result = await client.GetFromJsonAsync<SubmissionReviewDto>(ReviewUrl(ids));
        Assert.Equal(7m, result!.FinalGrade);
        Assert.Equal(result.SuggestedGrade, result.FinalGrade);
        using var scope = _fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == ids.SubmissionId && a.Action == "submission.finalize").ToListAsync());
    }

    [Fact]
    public async Task Summary_UsesPersistedMetadataAndStatusFilter()
    {
        var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        using (var scope = _fixture.ScopeFactory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var sub = await ctx.ExamSubmissions.FindAsync(ids.SubmissionId);
            sub!.ViolationCount = 3;
            await ctx.SaveChangesAsync();
        }
        var client = _host.As(Teacher);
        var list = await client.GetFromJsonAsync<SubmissionSummaryDto[]>($"/api/Exam/{ids.ExamId}/submissions?status=PendingReview");
        var summary = Assert.Single(list!);
        Assert.Equal(3, summary.ViolationCount);
        Assert.Equal(SubmitReason.Manual, summary.SubmitReason);
        var other = await client.GetFromJsonAsync<SubmissionSummaryDto[]>($"/api/Exam/{ids.ExamId}/submissions?status=Finalized");
        Assert.Empty(other!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/Exam/{ids.ExamId}/submissions?status=99")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/Exam/{ids.ExamId}/submissions?status=unknown")).StatusCode);
    }

    [Fact]
    public async Task MissingRoleIdentity_ForbiddenInsteadOfUnrestrictedQueries()
    {
        var ids = await _fixture.ResetScenarioAsync(IntegrationScenario.ExamRegression);
        var missingStudent = new ClaimsContext(ids.StudentId, UserRole.Student, null, null);
        Assert.Equal(HttpStatusCode.Forbidden, (await _host.As(missingStudent).GetAsync(ResultUrl(ids))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _host.As(missingStudent).GetAsync("/api/Grade")).StatusCode);
        var missingTeacher = new ClaimsContext(SeedData.TeacherUserId, UserRole.Teacher, null, null);
        Assert.Equal(HttpStatusCode.Forbidden, (await _host.As(missingTeacher).GetAsync(ReviewUrl(ids))).StatusCode);
    }

    [Fact]
    public async Task InvalidInputsAndMissingResources_UseSafeHttpErrors()
    {
        var ids = await _fixture.PreparePendingReviewScenarioAsync(IntegrationScenario.ExamRegression);
        var client = _host.As(Teacher);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(FinalizeUrl(ids), new FinalizeSubmissionDto())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(ReviewUrl(ids), await ReviewDtoAsync(ids))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(FinalizeUrl(ids), new FinalizeSubmissionDto { FinalGrade = 8m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(FinalizeUrl(ids), new FinalizeSubmissionDto { FinalGrade = 11m, OverrideReason = "Reason" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(ReviewUrl(ids), new { Answers = (object?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(ReviewUrl(ids), new { Answers = new object?[] { null } })).StatusCode);
        var reviewed = await client.GetFromJsonAsync<SubmissionReviewDto>(ReviewUrl(ids));
        var closedAnswer = reviewed!.Answers.First(a => a.QuestionType != QuestionType.Open);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(ReviewUrl(ids), new ReviewOpenAnswersDto
        {
            Answers = { new OpenAnswerReviewInputDto { AnswerId = closedAnswer.AnswerId, AwardedPoints = 0m } }
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Exam/submission/{Guid.NewGuid()}/review")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.As(Admin).GetAsync($"/api/Exam/{Guid.NewGuid()}/submissions")).StatusCode);
        var internalError = await client.GetAsync("/test/internal-error");
        Assert.Equal(HttpStatusCode.InternalServerError, internalError.StatusCode);
        Assert.DoesNotContain("sensitive", await internalError.Content.ReadAsStringAsync());
        var conflict = await client.GetAsync("/test/domain-error");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.DoesNotContain("sensitive", await conflict.Content.ReadAsStringAsync());
    }
}
