using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public partial class Phase4_ExamLifecycleTests
{
    [Fact]
    public async Task RetakeAssignments_AreExplicitAndAdminCanReplaceThemInDraft()
    {
        var otherStudent = await fixture.ResetScenarioAsync(IntegrationScenario.Phase4Unassigned);
        var original = await CreateAsync(Draft());
        var create = Draft(original);
        create.AssignedStudents.Clear();
        var retake = await CreateAsync(create, Admin);
        Assert.Empty((await DetailAsync(retake))!.AssignedStudents);
        var update = Update((await DetailAsync(retake))!);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Admin).PutAsJsonAsync($"/api/Exam/{retake}", update)).StatusCode);
        var assignment = new UpdateExamStudentsDto { Students = new() { new() { StudentId = otherStudent.StudentId } } };
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Admin).PutAsJsonAsync($"/api/Exam/{retake}/students", assignment)).StatusCode);
        Assert.Equal(otherStudent.StudentId, Assert.Single((await DetailAsync(retake))!.AssignedStudents).StudentId);
        Assert.Equal(_ids.StudentId, Assert.Single((await DetailAsync(original))!.AssignedStudents).StudentId);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Admin).PostAsync($"/api/Exam/{retake}/publish", null)).StatusCode);
        Assert.DoesNotContain((await _host.As(Phase3.Student(_ids)).GetFromJsonAsync<AssignedExamDto[]>("/api/Exam/assigned"))!, e => e.Id == retake);
        Assert.Contains((await _host.As(Phase3.Student(otherStudent)).GetFromJsonAsync<AssignedExamDto[]>("/api/Exam/assigned"))!, e => e.Id == retake);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("closed")]
    [InlineData("cancelled")]
    [InlineData("future")]
    [InlineData("expired")]
    public async Task StudentStart_RequiresPublishedAndCurrentWindow(string state)
    {
        var dto = Draft();
        if (state == "future") { dto.AvailableFrom = DateTime.UtcNow.AddDays(1); dto.AvailableUntil = dto.AvailableFrom.AddHours(2); }
        var id = await CreateAsync(dto);
        if (state != "draft")
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/publish", null)).StatusCode);
        if (state is "closed" or "cancelled")
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/{(state == "closed" ? "close" : "cancel")}", null)).StatusCode);
        if (state == "expired")
        {
            using var scope = fixture.ScopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var exam = await ctx.Exams.FindAsync(id);
            exam!.AvailableFrom = DateTime.UtcNow.AddHours(-3);
            exam.AvailableUntil = DateTime.UtcNow.AddHours(-1);
            await ctx.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Phase3.Student(_ids)).PostAsync($"/api/Exam/{id}/start", null)).StatusCode);
        using var verify = fixture.ScopeFactory.CreateScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().ExamSubmissions.AnyAsync(s => s.ExamId == id));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("read")]
    [InlineData("update")]
    [InlineData("students")]
    [InlineData("publish")]
    [InlineData("close")]
    [InlineData("cancel")]
    [InlineData("delete")]
    public async Task LifecycleHttp_RejectsAnonymousStudentAndNonOwner(string operation)
    {
        var id = await CreateAsync(Draft());
        var detail = (await DetailAsync(id))!;
        var before = JsonSerializer.Serialize(detail);
        foreach (var caller in new ClaimsContext?[] { null, Phase3.Student(_ids), Other })
        {
            var client = _host.As(caller);
            var response = operation switch
            {
                "create" => await client.PostAsJsonAsync("/api/Exam", Draft(id)),
                "read" => await client.GetAsync($"/api/Exam/{id}"),
                "update" => await client.PutAsJsonAsync($"/api/Exam/{id}", Update(detail)),
                "students" => await client.PutAsJsonAsync($"/api/Exam/{id}/students", new UpdateExamStudentsDto()),
                "delete" => await client.DeleteAsync($"/api/Exam/{id}"),
                _ => await client.PostAsync($"/api/Exam/{id}/{operation}", null)
            };
            Assert.Equal(caller is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(before, JsonSerializer.Serialize(await DetailAsync(id)));
        var othersList = await _host.As(Other).GetFromJsonAsync<ExamSummaryDto[]>("/api/Exam");
        Assert.DoesNotContain(othersList!, e => e.Id == id);
    }

    [Fact]
    public async Task AssignedRetake_VisibleOnlyToAssignedStudent_AndCancelledCannotStart()
    {
        var otherStudent = await fixture.ResetScenarioAsync(IntegrationScenario.Phase4Unassigned);
        var original = await CreateAsync(Draft());
        var retake = await CreateAsync(Draft(original));
        Assert.Empty((await _host.As(Phase3.Student(_ids)).GetFromJsonAsync<AssignedExamDto[]>("/api/Exam/assigned"))!.Where(e => e.Id == retake));
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{retake}/publish", null)).StatusCode);
        Assert.Contains((await _host.As(Phase3.Student(_ids)).GetFromJsonAsync<AssignedExamDto[]>("/api/Exam/assigned"))!, e => e.Id == retake && e.IsRetake);
        Assert.DoesNotContain((await _host.As(Phase3.Student(otherStudent)).GetFromJsonAsync<AssignedExamDto[]>("/api/Exam/assigned"))!, e => e.Id == retake);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.As(Phase3.Student(otherStudent)).PostAsync($"/api/Exam/{retake}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _host.As(Teacher).PostAsync($"/api/Exam/{retake}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _host.As(null).PostAsync($"/api/Exam/{retake}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Admin).PostAsync($"/api/Exam/{retake}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Phase3.Student(_ids)).PostAsync($"/api/Exam/{retake}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Admin).PostAsync($"/api/Exam/{retake}/cancel", null)).StatusCode);
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var audit = Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == retake && a.Action == "exam.cancel").ToListAsync());
        Assert.Equal(SeedData.AdminUserId, audit.ActorUserId);
        Assert.Equal("exam", audit.EntityType);
        using var before = JsonDocument.Parse(audit.BeforeJson!);
        using var after = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal((int)ExamStatus.Published, before.RootElement.GetProperty("Status").GetInt32());
        Assert.Equal((int)ExamStatus.Cancelled, after.RootElement.GetProperty("Status").GetInt32());
        Assert.False(await ctx.ExamSubmissions.AnyAsync(s => s.ExamId == retake));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("schedule")]
    [InlineData("duration")]
    [InlineData("passing")]
    [InlineData("questions")]
    [InlineData("points")]
    [InlineData("options")]
    [InlineData("position")]
    public async Task InvalidCreateInput_Returns400WithoutPersistingExam(string kind)
    {
        var dto = Draft();
        if (kind == "name") dto.Name = " ";
        if (kind == "schedule") dto.AvailableUntil = dto.AvailableFrom;
        if (kind == "duration") dto.DurationMinutes = 5;
        if (kind == "passing") dto.PassingPercent = 100;
        if (kind == "questions") dto.Questions = null!;
        if (kind == "points") dto.Questions[0].Points = 0;
        if (kind == "options") dto.Questions[0].Type = QuestionType.Single;
        if (kind == "position") dto.Questions.Add(new() { Text = "Duplicate position", Position = 1, Type = QuestionType.Open, Points = 5 });
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var count = await ctx.Exams.CountAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await _host.As(Teacher).PostAsJsonAsync("/api/Exam", dto)).StatusCode);
        Assert.Equal(count, await ctx.Exams.CountAsync());
    }

    [Theory]
    [InlineData("total", 409)]
    [InlineData("expired", 409)]
    [InlineData("students", 409)]
    [InlineData("options", 400)]
    [InlineData("passing", 400)]
    public async Task Publish_RevalidatesPersistedDraft_AndFailureLeavesNoAudit(string kind, int status)
    {
        var id = await CreateAsync(Draft());
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var exam = await ctx.Exams.FindAsync(id);
            if (kind == "total") exam!.TotalPoints = 99m;
            if (kind == "expired") { exam!.AvailableFrom = DateTime.UtcNow.AddHours(-3); exam.AvailableUntil = DateTime.UtcNow.AddHours(-1); }
            if (kind == "students") await ctx.ExamStudents.Where(s => s.ExamId == id).ExecuteDeleteAsync();
            if (kind == "options") (await ctx.Questions.SingleAsync(q => q.ExamId == id)).Type = "single";
            if (kind == "passing") exam!.PassingPercent = 100m;
            await ctx.SaveChangesAsync();
        }
        var before = await ExamSnapshotAsync(id);
        Assert.Equal((HttpStatusCode)status, (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/publish", null)).StatusCode);
        Assert.Equal(before, await ExamSnapshotAsync(id));
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task LifecycleAudit_FaultAfterAuditSql_RollsBackStateAndAudit(string operation)
    {
        var id = await CreateAsync(Draft());
        if (operation != "publish")
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/publish", null)).StatusCode);
        var before = await ExamSnapshotAsync(id);
        bool sawWrittenAudit = false;
        var probe = new Phase3.SaveProbe { AfterSave = async ctx =>
        {
            if (!ctx.AuditEvents.Local.Any(a => a.EntityId == id && a.Action == "exam." + operation)) return;
            sawWrittenAudit = await ctx.AuditEvents.AsNoTracking().AnyAsync(a => a.EntityId == id && a.Action == "exam." + operation);
            var writtenState = await ctx.Exams.AsNoTracking().Where(e => e.Id == id).Select(e => e.Status).SingleAsync();
            Assert.Equal(operation == "publish" ? "published" : operation == "close" ? "closed" : "cancelled", writtenState);
            throw new Phase3.InjectedFailure();
        }};
        await using (var provider = Phase3.Provider(fixture, null, probe))
        using (var scope = provider.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IExamManager>();
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => operation switch
            {
                "publish" => manager.PublishAsync(id, Teacher),
                "close" => manager.CloseAsync(id, Teacher),
                _ => manager.CancelAsync(id, Teacher)
            });
        }
        Assert.True(sawWrittenAudit);
        Assert.Equal(2, probe.Saves);
        Assert.Equal(before, await ExamSnapshotAsync(id));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task DraftUpdate_FaultAfterSql_RestoresStructureAndProperties(int failOnSave)
    {
        var id = await CreateAsync(Draft());
        var dto = Update((await DetailAsync(id))!);
        dto.Questions[0].Points = 15m;
        var before = await ExamSnapshotAsync(id);
        var probe = new Phase3.SaveProbe();
        probe.AfterSave = async ctx =>
        {
            if (probe.Saves != failOnSave) return;
            if (failOnSave == 1) Assert.Equal(0, await ctx.Questions.AsNoTracking().CountAsync(q => q.ExamId == id));
            else Assert.Equal(dto.Name, await ctx.Exams.AsNoTracking().Where(e => e.Id == id).Select(e => e.Name).SingleAsync());
            throw new Phase3.InjectedFailure();
        };
        await using (var provider = Phase3.Provider(fixture, null, probe))
        using (var scope = provider.CreateScope())
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => scope.ServiceProvider.GetRequiredService<IExamManager>().UpdateAsync(id, dto, Teacher));
        Assert.Equal(failOnSave, probe.Saves);
        Assert.Equal(before, await ExamSnapshotAsync(id));
    }

    [Fact]
    public async Task AssignmentUpdate_FaultAfterSql_RestoresPreviousExtraTime()
    {
        var id = await CreateAsync(Draft());
        var before = await ExamSnapshotAsync(id);
        var probe = new Phase3.SaveProbe { AfterSave = async ctx =>
        {
            Assert.Equal(15, await ctx.ExamStudents.AsNoTracking().Where(s => s.ExamId == id).Select(s => s.ExtraTimeMinutes).SingleAsync());
            throw new Phase3.InjectedFailure();
        }};
        await using (var provider = Phase3.Provider(fixture, null, probe))
        using (var scope = provider.CreateScope())
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(() => scope.ServiceProvider.GetRequiredService<IExamManager>().UpdateStudentsAsync(id,
                new UpdateExamStudentsDto { Students = new() { new() { StudentId = _ids.StudentId, ExtraTimeMinutes = 15 } } }, Teacher));
        Assert.Equal(before, await ExamSnapshotAsync(id));
    }

    [Fact]
    public async Task MissingResourcesAndDraftWithSubmission_ReturnAppropriateErrors()
    {
        var missing = Guid.NewGuid();
        foreach (var operation in new[] { "publish", "close", "cancel" })
            Assert.Equal(HttpStatusCode.NotFound, (await _host.As(Teacher).PostAsync($"/api/Exam/{missing}/{operation}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.As(Teacher).GetAsync($"/api/Exam/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.As(Teacher).PostAsJsonAsync("/api/Exam", Draft(missing))).StatusCode);
        using (var scope = fixture.ScopeFactory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            (await ctx.Exams.FindAsync(_ids.ExamId))!.Status = "draft";
            await ctx.SaveChangesAsync();
        }
        var before = await Phase3.SnapshotAsync(fixture.ScopeFactory, _ids);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).DeleteAsync($"/api/Exam/{_ids.ExamId}")).StatusCode);
        Assert.Equal(before, await Phase3.SnapshotAsync(fixture.ScopeFactory, _ids));
        var error = await _host.As(Teacher).GetAsync("/test/internal-error");
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.DoesNotContain("sensitive", await error.Content.ReadAsStringAsync());
    }

    private async Task<string> ExamSnapshotAsync(Guid id)
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var exam = await scope.ServiceProvider.GetRequiredService<IExamRepository>().GetByIdAsync(id, true, true);
        var audits = await ctx.AuditEvents.AsNoTracking().Where(a => a.EntityId == id).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Action, a.ActorUserId, a.BeforeJson, a.AfterJson, a.CreatedAt }).ToListAsync();
        return JsonSerializer.Serialize(new { exam, audits });
    }

    [Fact]
    public async Task ConcurrentPublish_ProducesOneTransitionAndOneAudit()
    {
        var id = await CreateAsync(Draft());
        await Phase3.OrderedAsync(fixture,
            sp => sp.GetRequiredService<IExamManager>().PublishAsync(id, Teacher),
            async sp => await Assert.ThrowsAsync<ElectronicRegisterAPI.Domain.Exceptions.BusinessRuleException>(() =>
                sp.GetRequiredService<IExamManager>().PublishAsync(id, Teacher)));
        Assert.Equal(ExamStatus.Published, (await DetailAsync(id))!.Status);
        using var scope = fixture.ScopeFactory.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().AuditEvents
            .Where(a => a.EntityId == id && a.Action == "exam.publish").ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelAndStart_AreSerializedWithoutCancelledExamAttempts(bool cancelFirst)
    {
        var id = await CreateAsync(Draft());
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/publish", null)).StatusCode);
        Task Cancel(IServiceProvider sp) => sp.GetRequiredService<IExamManager>().CancelAsync(id, Teacher);
        Task Start(IServiceProvider sp) => sp.GetRequiredService<IExamManager>().StartAsync(id, Phase3.Student(_ids));
        await Phase3.OrderedAsync(fixture, cancelFirst ? Cancel : Start, async sp =>
            await Assert.ThrowsAsync<ElectronicRegisterAPI.Domain.Exceptions.BusinessRuleException>(() => cancelFirst ? Start(sp) : Cancel(sp)));
        Assert.Equal(cancelFirst ? ExamStatus.Cancelled : ExamStatus.Published, (await DetailAsync(id))!.Status);
        using var scope = fixture.ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal(cancelFirst ? 0 : 1, await ctx.ExamSubmissions.CountAsync(s => s.ExamId == id));
        Assert.Equal(cancelFirst ? 1 : 0, await ctx.AuditEvents.CountAsync(a => a.EntityId == id && a.Action == "exam.cancel"));
    }
}
