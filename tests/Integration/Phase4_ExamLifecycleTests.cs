using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Exceptions;
using System.Text.Json;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public partial class Phase4_ExamLifecycleTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private ScenarioIds _ids = null!;
    private Phase2C_HttpHost _host = null!;
    private readonly List<Guid> _created = new();
    private static FixtureActor Teacher => Phase3.Teacher;
    private static FixtureActor Admin => new(SeedData.AdminUserId, null, null);
    private static FixtureActor Other => new(SeedData.Id(0, 13), null, SeedData.OtherTeacherId);

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        _ids = await fixture.ResetScenarioAsync(IntegrationScenario.Phase4Lifecycle);
        _host = await Phase2C_HttpHost.StartAsync(fixture.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_host is not null) await _host.DisposeAsync();
        if (_ids is null) return;
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        await using var tx = await ctx.Database.BeginTransactionAsync();
        var submissions = await ctx.ExamSubmissions.Where(s => _created.Contains(s.ExamId)).Select(s => s.Id).ToListAsync();
        var answers = await ctx.SubmissionAnswers.Where(a => submissions.Contains(a.SubmissionId)).Select(a => a.Id).ToListAsync();
        await ctx.Set<Dictionary<string, object>>("SubmissionAnswerOption")
            .Where(j => answers.Contains(EF.Property<Guid>(j, "SubmissionAnswerId"))).ExecuteDeleteAsync();
        await ctx.SubmissionAnswers.Where(a => submissions.Contains(a.SubmissionId)).ExecuteDeleteAsync();
        await ctx.ExamViolations.Where(v => submissions.Contains(v.SubmissionId)).ExecuteDeleteAsync();
        var gradeIds = await ctx.ExamSubmissions.Where(s => submissions.Contains(s.Id) && s.GradeId.HasValue).Select(s => s.GradeId!.Value).ToListAsync();
        await ctx.AuditEvents.Where(a => a.EntityId.HasValue && (submissions.Contains(a.EntityId.Value) || _created.Contains(a.EntityId.Value))).ExecuteDeleteAsync();
        await ctx.ExamSubmissions.Where(s => submissions.Contains(s.Id)).ExecuteDeleteAsync();
        await ctx.Grades.Where(g => gradeIds.Contains(g.Id)).ExecuteDeleteAsync();
        await ctx.Exams.Where(e => _created.Contains(e.Id)).ExecuteDeleteAsync();
        await tx.CommitAsync();
    }

    private CreateExamDto Draft(Guid? original = null) => new()
    {
        ClassSubjectId = SeedData.ClassSubjectId, RetakeOfExamId = original, Name = "Phase 4 exam",
        AvailableFrom = DateTime.UtcNow.AddMinutes(-1), AvailableUntil = DateTime.UtcNow.AddHours(2), DurationMinutes = 30,
        AssignedStudents = new() { new() { StudentId = _ids.StudentId, ExtraTimeMinutes = 5 } },
        Questions = new() { new() { Position = 1, Text = "Open question", Type = QuestionType.Open, Points = 10 } }
    };

    private async Task<Guid> CreateAsync(CreateExamDto dto, FixtureActor? caller = null)
    {
        var response = await _host.As(caller ?? Teacher).PostAsJsonAsync("/api/institution/exams", dto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        _created.Add(id);
        Assert.NotNull(response.Headers.Location);
        return id;
    }

    private Task<ExamDetailDto?> DetailAsync(Guid id) => _host.As(Teacher).GetFromJsonAsync<ExamDetailDto>($"/api/institution/exams/{id}");
    private static UpdateExamDto Update(ExamDetailDto dto) => new()
    {
        ClassSubjectId = dto.ClassSubjectId, RetakeOfExamId = dto.RetakeOfExamId, Name = "Updated draft",
        AvailableFrom = dto.AvailableFrom, AvailableUntil = dto.AvailableUntil, DurationMinutes = dto.DurationMinutes,
        PassingPercent = dto.PassingPercent, Questions = dto.Questions.Select(q => new QuestionInputDto
        {
            Position = q.Position, Text = q.Text, Type = q.Type, Points = q.Points, Required = q.Required,
            Options = q.Options.Select(o => new QuestionOptionInputDto { Position = o.Position, Text = o.Text, IsCorrect = o.IsCorrect }).ToList()
        }).ToList()
    };

    [Fact]
    public async Task DraftUpdate_ApplicationPersistsStructureAndLink()
    {
        var original = await CreateAsync(Draft());
        var id = await CreateAsync(Draft());
        var dto = Update((await DetailAsync(id))!);
        dto.RetakeOfExamId = original;
        using var scope = fixture.ScopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FixtureExamClient>().UpdateAsync(id, dto, Teacher);
        Assert.Equal(original, (await DetailAsync(id))!.RetakeOfExamId);
    }

    [Fact]
    public async Task DraftUpdate_PersistsRetakeLinkAndRejectsClassSubjectChange()
    {
        var original = await CreateAsync(Draft());
        var retake = await CreateAsync(Draft());
        var update = Update((await DetailAsync(retake))!);
        update.RetakeOfExamId = original;
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{retake}", update)).StatusCode);
        Assert.Equal(original, (await DetailAsync(retake))!.RetakeOfExamId);
        update.ClassSubjectId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{retake}", update)).StatusCode);
    }

    [Fact]
    public async Task UpdateAssignments_UsesClassIdAndPersistsExtraTime()
    {
        var id = await CreateAsync(Draft());
        var dto = new UpdateExamStudentsDto { Students = new() { new() { StudentId = _ids.StudentId, ExtraTimeMinutes = 15 } } };
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{id}/students", dto)).StatusCode);
        Assert.Equal(15, Assert.Single((await DetailAsync(id))!.AssignedStudents).ExtraTimeMinutes);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("cancelled")]
    public async Task Close_RejectsIllegalSourceState(string state)
    {
        var id = await CreateAsync(Draft());
        if (state == "cancelled")
        {
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/publish", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/cancel", null)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/close", null)).StatusCode);
    }

    [Fact]
    public async Task Publish_PersistsSingleAtomicLifecycleAudit()
    {
        var id = await CreateAsync(Draft());
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Admin).PostAsync($"/api/institution/exams/{id}/publish", null)).StatusCode);
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var audit = Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == id && a.Action == "exam.publish").ToListAsync());
        Assert.Equal(SeedData.AdminUserId, audit.ActorUserId);
        Assert.NotNull(audit.BeforeJson);
        Assert.NotNull(audit.AfterJson);
    }

    [Fact]
    public async Task RetakeLinks_RejectSelfCyclesMissingAndOtherClassSubject_WithoutChanges()
    {
        var original = await CreateAsync(Draft());
        var child = await CreateAsync(Draft(original));
        var grandchild = await CreateAsync(Draft(child));
        var before = JsonSerializer.Serialize(await DetailAsync(original));
        foreach (var target in new[] { original, grandchild, Guid.NewGuid(), _ids.ExamId })
        {
            var dto = Update((await DetailAsync(original))!);
            dto.RetakeOfExamId = target;
            if (target == _ids.ExamId)
            {
                // A separate ClassSubject is test data, not a modified shared ClassSubject.
                using var scope = fixture.ScopeFactory.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
                var otherSubject = new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Subject { Id = Guid.NewGuid(), Name = "Phase4 other subject" };
                var otherClassSubject = new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.ClassSubject
                {
                    Id = Guid.NewGuid(), ClassId = SeedData.ClassId, TeacherId = SeedData.TeacherId, SubjectId = otherSubject.Id
                };
                ctx.Subjects.Add(otherSubject);
                ctx.ClassSubjects.Add(otherClassSubject);
                SeedData.StampNewOwnedRows(ctx);
                var entity = await ctx.Exams.FindAsync(_ids.ExamId);
                entity!.ClassSubjectId = otherClassSubject.Id;
                await ctx.SaveChangesAsync();
                try
                {
                    Assert.Equal(HttpStatusCode.BadRequest, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{original}", dto)).StatusCode);
                    Assert.Equal(HttpStatusCode.BadRequest, (await _host.As(Teacher).PostAsJsonAsync("/api/institution/exams", Draft(_ids.ExamId))).StatusCode);
                }
                finally
                {
                    entity.ClassSubjectId = SeedData.ClassSubjectId;
                    await ctx.SaveChangesAsync();
                    ctx.ClassSubjects.Remove(otherClassSubject);
                    ctx.Subjects.Remove(otherSubject);
                    await ctx.SaveChangesAsync();
                }
            }
            else
                Assert.Equal(target == original || target == grandchild ? HttpStatusCode.BadRequest : HttpStatusCode.NotFound,
                    (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{original}", dto)).StatusCode);
            Assert.Equal(before, JsonSerializer.Serialize(await DetailAsync(original)));
        }
        var remove = Update((await DetailAsync(child))!);
        remove.RetakeOfExamId = null;
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{child}", remove)).StatusCode);
        Assert.False((await DetailAsync(child))!.IsRetake);
    }

    [Theory]
    [InlineData("duplicate", 400)]
    [InlineData("negative", 400)]
    [InlineData("missing", 404)]
    [InlineData("null", 400)]
    [InlineData("wrong-class", 400)]
    public async Task InvalidAssignments_AreRejectedWithoutChanges(string kind, int status)
    {
        var id = await CreateAsync(Draft());
        var before = JsonSerializer.Serialize(await DetailAsync(id));
        var assignments = new UpdateExamStudentsDto { Students = new() { new() { StudentId = _ids.StudentId } } };
        if (kind == "duplicate") assignments.Students.Add(new() { StudentId = _ids.StudentId });
        if (kind == "negative") assignments.Students[0].ExtraTimeMinutes = -1;
        if (kind == "missing") assignments.Students[0].StudentId = Guid.NewGuid();
        if (kind == "null") assignments.Students.Add(null!);
        Guid? previousClass = null;
        if (kind == "wrong-class")
        {
            using var scope = fixture.ScopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var student = await ctx.Students.FindAsync(_ids.StudentId);
            previousClass = student!.ClassId;
            student.ClassId = null;
            await ctx.SaveChangesAsync();
        }
        try
        {
            Assert.Equal((HttpStatusCode)status, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{id}/students", assignments)).StatusCode);
            var create = Draft();
            create.AssignedStudents = assignments.Students;
            Assert.Equal((HttpStatusCode)status, (await _host.As(Teacher).PostAsJsonAsync("/api/institution/exams", create)).StatusCode);
        }
        finally
        {
            if (kind == "wrong-class")
            {
                using var scope = fixture.ScopeFactory.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
                (await ctx.Students.FindAsync(_ids.StudentId))!.ClassId = previousClass;
                await ctx.SaveChangesAsync();
            }
        }
        Assert.Equal(before, JsonSerializer.Serialize(await DetailAsync(id)));
    }

    [Theory]
    [InlineData("published")]
    [InlineData("closed")]
    [InlineData("cancelled")]
    public async Task NonDraft_RejectsUpdatesPublishAndDelete(string state)
    {
        var id = await CreateAsync(Draft());
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/publish", null)).StatusCode);
        if (state != "published")
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/{(state == "closed" ? "close" : "cancel")}", null)).StatusCode);
        var before = JsonSerializer.Serialize(await DetailAsync(id));
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{id}", Update((await DetailAsync(id))!))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/exams/{id}/students", new UpdateExamStudentsDto())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).DeleteAsync($"/api/institution/exams/{id}")).StatusCode);
        if (state == "closed")
        {
            Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/close", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/cancel", null)).StatusCode);
        }
        Assert.Equal(before, JsonSerializer.Serialize(await DetailAsync(id)));
    }

    [Fact]
    public async Task DeleteReferencedOriginal_IsConflict_AndDeleteUnreferencedDraftIsSafe()
    {
        var original = await CreateAsync(Draft());
        var child = await CreateAsync(Draft(original));
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).DeleteAsync($"/api/institution/exams/{original}")).StatusCode);
        Assert.Equal(original, (await DetailAsync(child))!.RetakeOfExamId);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).DeleteAsync($"/api/institution/exams/{child}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).DeleteAsync($"/api/institution/exams/{original}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.As(Teacher).GetAsync($"/api/institution/exams/{original}")).StatusCode);
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.False(await ctx.Questions.AnyAsync(q => q.ExamId == original || q.ExamId == child));
        Assert.False(await ctx.ExamStudents.AnyAsync(s => s.ExamId == original || s.ExamId == child));
    }

    [Fact]
    public async Task CloseWithActiveSubmission_IsRejected_AndClosesAfterAutomaticDelivery()
    {
        var id = await CreateAsync(Draft());
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/publish", null)).StatusCode);
        var startResponse = await _host.As(Phase3.Student(_ids)).PostAsync($"/api/institution/exams/{id}/start", null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var started = (await startResponse.Content.ReadFromJsonAsync<StartExamDto>())!;
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            Assert.Equal(SubmissionStatus.InProgress, (await repo.GetByIdAsync(started.SubmissionId))!.Status);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/cancel", null)).StatusCode);
        var before = await ExamSnapshotAsync(id);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/close", null)).StatusCode);
        Assert.Equal(before, await ExamSnapshotAsync(id));
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            var sub = await scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>().GetByIdAsync(started.SubmissionId);
            Assert.Equal(SubmissionStatus.InProgress, sub!.Status);
            Assert.Null(sub.SubmittedAt);
        }
        await using (var provider = Phase3.Provider(fixture, new Phase3.Clock(started.DeadlineAt.AddSeconds(1))))
        using (var scope = provider.CreateScope())
        {
            var result = await FixtureTenant.For(scope.ServiceProvider, _ids.StudentId).SubmitExpiredAsync(started.SubmissionId);
            Assert.NotNull(result.SubmittedAt);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{id}/close", null)).StatusCode);
        using var verify = fixture.ScopeFactory.CreateScope();
        var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == id && a.Action == "exam.close").ToListAsync());
    }

    [Fact]
    public async Task RetakeFullHttpFlow_KeepsOriginalGradeAndSubmissionIndependent()
    {
        var original = await CreateAsync(Draft());
        var originalSubmission = await CompleteAsync(original, 4.8m);
        var originalBefore = await SubmissionJsonAsync(originalSubmission);
        var retake = await CreateAsync(Draft(original));
        Assert.Equal(original, (await DetailAsync(retake))!.RetakeOfExamId);
        var summary = await _host.As(Teacher).GetFromJsonAsync<ExamSummaryDto[]>("/api/institution/exams");
        Assert.Contains(summary!, e => e.Id == retake && e.IsRetake && e.AssignedStudentsCount == 1);
        var retakeSubmission = await CompleteAsync(retake, 9m);
        Assert.NotEqual(originalSubmission, retakeSubmission);
        Assert.Equal(originalBefore, await SubmissionJsonAsync(originalSubmission));
        var result = await _host.As(Phase3.Student(_ids)).GetFromJsonAsync<SubmissionResultDto>($"/api/institution/submissions/{retakeSubmission}/result");
        Assert.Equal(9m, result!.Grade);
        Assert.True(result.Passed);
        var assigned = await _host.As(Phase3.Student(_ids)).GetFromJsonAsync<AssignedExamDto[]>("/api/institution/exams/assigned");
        Assert.Contains(assigned!, e => e.Id == retake && e.IsRetake && !e.CanStart);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Phase3.Student(_ids)).PostAsync($"/api/institution/exams/{original}/start", null)).StatusCode);
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var submissions = await ctx.ExamSubmissions.Where(s => s.Id == originalSubmission || s.Id == retakeSubmission).ToListAsync();
        Assert.Equal(2, submissions.Select(s => s.GradeId).Distinct().Count());
        var originalGrade = await ctx.Grades.FindAsync(submissions.Single(s => s.Id == originalSubmission).GradeId);
        Assert.Equal(5m, originalGrade!.Value);
        foreach (var submission in submissions)
        {
            Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == submission.Id && a.Action == "submission.submit").ToListAsync());
            Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == submission.Id && a.Action == "submission.finalize").ToListAsync());
        }
    }

    private async Task<string> SubmissionJsonAsync(Guid id)
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        return JsonSerializer.Serialize(await scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>().GetByIdAsync(id, true));
    }

    private async Task<Guid> CompleteAsync(Guid examId, decimal points)
    {
        var detail = (await DetailAsync(examId))!;
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/institution/exams/{examId}/publish", null)).StatusCode);
        var assigned = await _host.As(Phase3.Student(_ids)).GetFromJsonAsync<AssignedExamDto[]>("/api/institution/exams/assigned");
        Assert.Contains(assigned!, e => e.Id == examId && e.CanStart);
        var startResponse = await _host.As(Phase3.Student(_ids)).PostAsync($"/api/institution/exams/{examId}/start", null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var start = (await startResponse.Content.ReadFromJsonAsync<StartExamDto>())!;
        Assert.Equal(TimeSpan.FromMinutes(35), start.DeadlineAt - start.StartedAt);
        var save = new SaveExamAnswersDto { ClientRevision = 1, Answers = new() { new() { QuestionId = detail.Questions[0].Id, OpenText = "Student work" } } };
        Assert.Equal(HttpStatusCode.OK, (await _host.As(Phase3.Student(_ids)).PutAsJsonAsync($"/api/institution/submissions/{start.SubmissionId}/answers", save)).StatusCode);
        var submit = new SubmitExamDto { ClientRevision = 2, Answers = save.Answers };
        var submitted = await _host.As(Phase3.Student(_ids)).PostAsJsonAsync($"/api/institution/submissions/{start.SubmissionId}/submit", submit);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Null((await submitted.Content.ReadFromJsonAsync<SubmissionResultDto>())!.Grade);
        var review = await _host.As(Teacher).GetFromJsonAsync<SubmissionReviewDto>($"/api/institution/submissions/{start.SubmissionId}/review");
        var input = new ReviewOpenAnswersDto { Answers = new() { new() { AnswerId = review!.Answers.Single().AnswerId, AwardedPoints = points } } };
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PutAsJsonAsync($"/api/institution/submissions/{start.SubmissionId}/review", input)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsJsonAsync($"/api/institution/submissions/{start.SubmissionId}/finalize", new FinalizeSubmissionDto())).StatusCode);
        return start.SubmissionId;
    }
}
