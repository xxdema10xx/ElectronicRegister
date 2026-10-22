using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public partial class Phase4_ExamLifecycleTests
{
    [Theory]
    [InlineData("none", false)]
    [InlineData("in_progress", true)]
    [InlineData("pending_review", false)]
    [InlineData("graded", false)]
    [InlineData("finalized", false)]
    [InlineData("failed_auto", false)]
    [InlineData("expired", true)]
    [InlineData("mixed", true)]
    [InlineData("completed", false)]
    public async Task CloseBusinessRule_HttpAndMySql(string kind, bool blocked)
    {
        var id = await CreateAsync(Draft());
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/publish", null)).StatusCode);
        if (kind != "none")
        {
            using var setup = fixture.ScopeFactory.CreateScope();
            var ctx = setup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var source = await ctx.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == _ids.SubmissionId);
            source.Id = Guid.NewGuid();
            source.ExamId = id;
            source.Status = kind is "expired" or "mixed" ? "in_progress" : kind == "completed" ? "graded" : kind;
            source.DeadlineAt = kind == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(10);
            ctx.ExamSubmissions.Add(source);
            if (kind is "mixed" or "completed")
            {
                var other = await fixture.ResetScenarioAsync(IntegrationScenario.Phase4Unassigned);
                ctx.ExamSubmissions.Add(new()
                {
                    Id = Guid.NewGuid(), ExamId = id, StudentId = other.StudentId,
                    Status = kind == "mixed" ? "pending_review" : "finalized",
                    StartedAt = DateTime.UtcNow, DeadlineAt = DateTime.UtcNow.AddMinutes(10)
                });
            }
            await ctx.SaveChangesAsync();
        }
        var before = await ExamSnapshotAsync(id);
        var submissionsBefore = await CloseSubmissionsSnapshotAsync(id);
        Assert.Equal(blocked ? HttpStatusCode.Conflict : HttpStatusCode.NoContent,
            (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/close", null)).StatusCode);
        Assert.Equal(submissionsBefore, await CloseSubmissionsSnapshotAsync(id));
        if (blocked) Assert.Equal(before, await ExamSnapshotAsync(id));
        using var verify = fixture.ScopeFactory.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == id);
        Assert.Equal(blocked ? "published" : "closed", exam.Status);
        Assert.Equal(!blocked, exam.ClosedAt.HasValue);
        Assert.Equal(blocked ? 0 : 1, await db.AuditEvents.CountAsync(a => a.EntityId == id && a.Action == "exam.close"));
    }

    private async Task<string> CloseSubmissionsSnapshotAsync(Guid id)
    {
        using var scope = fixture.ScopeFactory.CreateScope();
        return System.Text.Json.JsonSerializer.Serialize(await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>()
            .ExamSubmissions.AsNoTracking().Where(s => s.ExamId == id).OrderBy(s => s.Id).ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseAndStart_AreSerializedInBothOrders(bool closeFirst)
    {
        var id = await CreateAsync(Draft());
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/publish", null)).StatusCode);
        Task Close(IServiceProvider sp) => sp.GetRequiredService<IExamManager>().CloseAsync(id, Teacher);
        Task Start(IServiceProvider sp) => sp.GetRequiredService<IExamManager>().StartAsync(id, Phase3.Student(_ids));
        await Phase3.OrderedAsync(fixture, closeFirst ? Close : Start, async sp =>
            await Assert.ThrowsAsync<BusinessRuleException>(() => closeFirst ? Start(sp) : Close(sp)));
        using var verify = fixture.ScopeFactory.CreateScope();
        var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var exam = await ctx.Exams.AsNoTracking().SingleAsync(e => e.Id == id);
        Assert.Equal(closeFirst ? "closed" : "published", exam.Status);
        Assert.Equal(closeFirst, exam.ClosedAt.HasValue);
        Assert.Equal(closeFirst ? 0 : 1, await ctx.ExamSubmissions.CountAsync(s => s.ExamId == id && s.Status == "in_progress"));
        Assert.Equal(closeFirst ? 1 : 0, await ctx.AuditEvents.CountAsync(a => a.EntityId == id && a.Action == "exam.close"));
    }
    [Fact]
    public async Task ExpiredLastSubmission_WorkerDeliveryAllowsHttpClose()
    {
        var ids = await StartBusinessExamAsync();
        var now = new DateTime(1901, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        using (var setup = fixture.ScopeFactory.CreateScope())
        {
            var ctx = setup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var sub = await ctx.ExamSubmissions.SingleAsync(s => s.Id == ids.SubmissionId);
            sub.StartedAt = now.AddHours(-1);
            sub.DeadlineAt = now.AddMinutes(-1);
            await ctx.SaveChangesAsync();
        }
        var before = await ExamSnapshotAsync(ids.ExamId);
        var subBefore = await Phase3.SnapshotAsync(fixture.ScopeFactory, ids);
        Assert.Equal(HttpStatusCode.Conflict, (await _host.As(Teacher).PostAsync($"/api/Exam/{ids.ExamId}/close", null)).StatusCode);
        Assert.Equal(before, await ExamSnapshotAsync(ids.ExamId));
        Assert.Equal(subBefore, await Phase3.SnapshotAsync(fixture.ScopeFactory, ids));
        await using (var provider = Phase3.Provider(fixture, new Phase3.Clock(now)))
        {
            var worker = new ElectronicRegisterAPI.Api.BackgroundServices.ExpiredExamSubmissionWorker(
                provider.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ElectronicRegisterAPI.Api.BackgroundServices.ExpiredExamSubmissionWorker>.Instance,
                new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string,string?> { ["Identity:Legacy:Enabled"] = "true" }).Build());
            await worker.ProcessBatchAsync(System.Threading.CancellationToken.None);
            worker.Dispose();
        }
        using (var verify = fixture.ScopeFactory.CreateScope())
        {
            var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var sub = await ctx.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == ids.SubmissionId);
            Assert.NotEqual("in_progress", sub.Status);
            Assert.Equal(now, sub.SubmittedAt);
            Assert.Equal("time_expired", sub.SubmitReason);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{ids.ExamId}/close", null)).StatusCode);
        using var final = fixture.ScopeFactory.CreateScope();
        var db = final.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == ids.ExamId);
        Assert.Equal("closed", exam.Status);
        Assert.NotNull(exam.ClosedAt);
        Assert.Single(await db.AuditEvents.Where(a => a.EntityId == ids.ExamId && a.Action == "exam.close").ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseDuringSubmit_PreservesInvariantAndAllowsRetry(bool automatic)
    {
        var ids = await StartBusinessExamAsync();
        var dto = new ElectronicRegisterAPI.Domain.DTOs.SubmitExamDto
        {
            ClientRevision = 1,
            Answers = new() { new() { QuestionId = ids.OpenQuestionId, OpenText = "Delivered work" } }
        };
        DateTime deadline;
        using (var scope = fixture.ScopeFactory.CreateScope())
            deadline = await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().ExamSubmissions
                .Where(s => s.Id == ids.SubmissionId).Select(s => s.DeadlineAt).SingleAsync();
        Exception? closeError = null;
        await Phase3.OrderedAsync(fixture,
            async sp => await sp.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Services.IExamSubmissionProcessor>()
                .ProcessSubmitAsync(ids.SubmissionId, automatic ? null : dto,
                    automatic ? ElectronicRegisterAPI.Domain.Enums.SubmitReason.TimeExpired : ElectronicRegisterAPI.Domain.Enums.SubmitReason.Manual,
                    automatic ? null : Phase3.Student(ids)),
            async sp => closeError = await Record.ExceptionAsync(() => sp.GetRequiredService<IExamManager>().CloseAsync(ids.ExamId, Teacher)),
            new Phase3.Clock(automatic ? deadline.AddSeconds(1) : deadline.AddSeconds(-1)));
        using (var check = fixture.ScopeFactory.CreateScope())
        {
            var db = check.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == ids.ExamId);
            Assert.False(await db.ExamSubmissions.AnyAsync(s => s.ExamId == ids.ExamId && s.Status == "in_progress"));
            Assert.Equal(closeError is null ? "closed" : "published", exam.Status);
            Assert.Equal(closeError is null, exam.ClosedAt.HasValue);
            Assert.Equal(closeError is null ? 1 : 0, await db.AuditEvents.CountAsync(a => a.EntityId == ids.ExamId && a.Action == "exam.close"));
        }
        if (closeError is not null)
        {
            Assert.IsType<BusinessRuleException>(closeError);
            Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{ids.ExamId}/close", null)).StatusCode);
        }
        using var verify = fixture.ScopeFactory.CreateScope();
        var ctx = verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal("closed", await ctx.Exams.Where(e => e.Id == ids.ExamId).Select(e => e.Status).SingleAsync());
        Assert.False(await ctx.ExamSubmissions.AnyAsync(s => s.ExamId == ids.ExamId && s.Status == "in_progress"));
        Assert.Single(await ctx.AuditEvents.Where(a => a.EntityId == ids.ExamId && a.Action == "exam.close").ToListAsync());
    }

    private async Task<ScenarioIds> StartBusinessExamAsync()
    {
        var id = await CreateAsync(Draft());
        Assert.Equal(HttpStatusCode.NoContent, (await _host.As(Teacher).PostAsync($"/api/Exam/{id}/publish", null)).StatusCode);
        using var scope = fixture.ScopeFactory.CreateScope();
        var started = await scope.ServiceProvider.GetRequiredService<IExamManager>().StartAsync(id, Phase3.Student(_ids));
        var detail = (await DetailAsync(id))!;
        return _ids with { ExamId = id, SubmissionId = started.SubmissionId, OpenQuestionId = detail.Questions.Single().Id };
    }
}
