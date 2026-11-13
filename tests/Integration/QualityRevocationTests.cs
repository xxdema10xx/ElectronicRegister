using System;
using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Theory]
    [InlineData("teacher", "update")]
    [InlineData("student", "answers")]
    [InlineData("admin", "grade")]
    [InlineData("staff", "update")]
    public async Task Quality_GlobalPasswordGenerationRevokesOpenedWrite(string role, string operation)
    {
        var prepared = await PrepareQualityWrite(role, operation);
        var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, [], enabled: false, school: true, exam: true,
            configureServices: services =>
            {
                var descriptor = services.Last(s => s.ServiceType == typeof(IInstitutionExamRepositoryFactory));
                services.AddScoped<IInstitutionExamRepositoryFactory>(sp => new QualityOpenBarrier((IInstitutionExamRepositoryFactory)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), opened, release));
            });
        var before = await Digest(false);
        var write = SqlLockEvidence.Track(() => Request(prepared.Route, prepared.Token, prepared.Body, prepared.Method, host));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await Expect(await Request("/api/identity/account/password", await Ticket(role == "staff" ? "teacher" : role),
                new ChangeIdentityPasswordDto(Password, "RotatedGlobally!"), "PUT"), HttpStatusCode.NoContent);
        }
        finally { release.TrySetResult(true); }
        await Expect(await write, HttpStatusCode.Forbidden);
        Assert.Equal(before, await Digest(false));
    }

    [Fact]
    public async Task Quality_LockEvidenceRejectsMissingIamLockMutant()
    {
        var prepared = await PrepareQualityWrite("teacher", "update");
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, [], enabled: false, school: true, exam: true,
            configureServices: services =>
            {
                var descriptor = services.Last(s => s.ServiceType == typeof(IInstitutionWriteAuthorization));
                services.AddScoped<IInstitutionWriteAuthorization>(sp => new QualityWriteBarrier((IInstitutionWriteAuthorization)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), acquired, release, omitLock: true));
            });
        var write = SqlLockEvidence.Track(() => Request(prepared.Route, prepared.Token, prepared.Body, prepared.Method, host));
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var revoke = SqlLockEvidence.Track(() => Execute("UPDATE institution_membership SET status='suspended' WHERE id=@m", ("@m", Id("teacher-membership"))));
        try
        {
            await revoke.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => SqlLockEvidence.WaitAsync(fixture.ConnectionString, revoke, write));
        }
        finally { release.TrySetResult(true); }
        await Expect(await write, HttpStatusCode.NoContent); // the deliberately unsafe test-only mutant
    }

    [Fact]
    public async Task Quality_ConcurrentCreatesDoNotUpgradeSharedTenantLocksOrDeadlock()
    {
        var token = await Access("teacher");
        var writes = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Request(Exams, token, StaffExamInput(), "POST")));
        foreach (var response in writes) await Expect(response, HttpStatusCode.Created);
        Assert.Equal(4, writes.Select(r => r.Headers.Location!.OriginalString).Distinct().Count());
    }

    [Fact]
    public async Task Quality_GlobalPasswordChangeWaitsForAuthorizedCommit()
    {
        var prepared = await PrepareQualityWrite("teacher", "update");
        var ticket = await Ticket("teacher");
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, [], enabled: false, school: true, exam: true,
            configureServices: services =>
            {
                var descriptor = services.Last(s => s.ServiceType == typeof(IInstitutionWriteAuthorization));
                services.AddScoped<IInstitutionWriteAuthorization>(sp => new QualityWriteBarrier((IInstitutionWriteAuthorization)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), acquired, release));
            });
        var write = SqlLockEvidence.Track(() => Request(prepared.Route, prepared.Token, prepared.Body, prepared.Method, host));
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var rotation = SqlLockEvidence.Track(() => Request("/api/identity/account/password", ticket, new ChangeIdentityPasswordDto(Password, "GloballyRotatedAgain!"), "PUT"));
        try { await SqlLockEvidence.WaitAsync(fixture.ConnectionString, rotation, write); }
        finally { release.TrySetResult(true); }
        await Expect(await write, HttpStatusCode.NoContent);
        await Expect(await rotation, HttpStatusCode.NoContent);
        await Expect(await Request(prepared.Route, prepared.Token, prepared.Body, prepared.Method), HttpStatusCode.Unauthorized);
    }
    [Theory]
    [InlineData("teacher", "update")]
    [InlineData("teacher", "publish")]
    [InlineData("admin", "update")]
    [InlineData("staff", "update")]
    [InlineData("student", "start")]
    [InlineData("student", "answers")]
    [InlineData("student", "submit")]
    [InlineData("teacher", "review")]
    [InlineData("teacher", "finalize")]
    [InlineData("teacher", "grade")]
    [InlineData("admin", "grade")]
    [InlineData("staff", "grade")]
    public Task Quality_RevocationCommittedAfterOpenPreventsWrite(string role, string operation) => AssertQualityRevocation(role, operation, false);

    [Theory]
    [InlineData("teacher", "publish")]
    [InlineData("student", "submit")]
    [InlineData("admin", "grade")]
    [InlineData("staff", "update")]
    public Task Quality_RoleRemovalCommittedAfterOpenPreventsWrite(string role, string operation) => AssertQualityRevocation(role, operation, true);

    private async Task AssertQualityRevocation(string role, string operation, bool removeRole)
    {
        var prepared = await PrepareQualityWrite(role, operation);
        var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, [], enabled: false, school: true, exam: true,
            configureServices: services =>
            {
                var descriptor = System.Linq.Enumerable.Last(services, s => s.ServiceType == typeof(IInstitutionExamRepositoryFactory));
                services.AddScoped<IInstitutionExamRepositoryFactory>(sp => new QualityOpenBarrier(
                    (IInstitutionExamRepositoryFactory)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), opened, release));
            });
        var before = await Digest(false);
        var write = SqlLockEvidence.Track(() => Request(prepared.Route, prepared.Token, prepared.Body, prepared.Method, host));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            // The paused request has passed manager authorization. Revocation is
            // committed on another SQL connection before its transaction may begin.
            var membership = Id((role == "staff" ? "teacher" : role) + "-membership");
            if (removeRole)
                await Execute("DELETE FROM institution_membership_role WHERE membership_id=@m AND role=@r", ("@m", membership),
                    ("@r", role == "teacher" ? "Teacher" : role == "student" ? "Student" : role == "staff" ? "Staff" : "InstitutionAdmin"));
            else
                await Execute("UPDATE institution_membership SET status='suspended' WHERE institution_id=@i AND id=@m", ("@i", _a), ("@m", membership));
        }
        finally { release.TrySetResult(true); }
        await Expect(await write, HttpStatusCode.Forbidden);
        Assert.Equal(before, await Digest(false));
    }

    [Theory]
    [InlineData("teacher", "update")]
    [InlineData("teacher", "publish")]
    [InlineData("admin", "update")]
    [InlineData("staff", "update")]
    [InlineData("student", "start")]
    [InlineData("student", "answers")]
    [InlineData("student", "submit")]
    [InlineData("teacher", "review")]
    [InlineData("teacher", "finalize")]
    [InlineData("teacher", "grade")]
    [InlineData("admin", "grade")]
    [InlineData("staff", "grade")]
    public async Task Quality_AuthorizedWriteHoldsIamUntilCommit(string role, string operation)
    {
        var prepared = await PrepareQualityWrite(role, operation);
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, [], enabled: false, school: true, exam: true,
            configureServices: services =>
            {
                var descriptor = services.Last(s => s.ServiceType == typeof(IInstitutionWriteAuthorization));
                services.AddScoped<IInstitutionWriteAuthorization>(sp => new QualityWriteBarrier(
                    (IInstitutionWriteAuthorization)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), acquired, release));
            });
        var write = SqlLockEvidence.Track(() => Request(prepared.Route, prepared.Token, prepared.Body, prepared.Method, host));
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var revoke = SqlLockEvidence.Track(() => Execute("UPDATE institution_membership SET status='suspended' WHERE institution_id=@i AND id=@m",
            ("@i", _a), ("@m", Id((role == "staff" ? "teacher" : role) + "-membership"))));
        try { await SqlLockEvidence.WaitAsync(fixture.ConnectionString, revoke, write); }
        finally { release.TrySetResult(true); }
        await Expect(await write, operation is "answers" or "submit" or "start" ? HttpStatusCode.OK : HttpStatusCode.NoContent);
        await revoke;
        await Expect(await Request(prepared.Route, prepared.Token, prepared.Body, prepared.Method), HttpStatusCode.Forbidden);
    }

    private sealed class QualityWriteBarrier(IInstitutionWriteAuthorization inner, TaskCompletionSource<bool> acquired, TaskCompletionSource<bool> release, bool omitLock = false) : IInstitutionWriteAuthorization
    {
        public Task LockAsync(TenantContext actor, CancellationToken ct = default) => inner.LockAsync(actor, ct);
        public async Task LockExamAsync(TenantContext actor, CancellationToken ct = default)
        {
            if (!omitLock) await inner.LockExamAsync(actor, ct);
            acquired.TrySetResult(true);
            await release.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
        }
        public void RequireLocked(TenantContext actor) { if (!omitLock) inner.RequireLocked(actor); }
    }

    private sealed record QualityRequest(string Route, string Token, object? Body, string Method);

    private async Task<QualityRequest> PrepareQualityWrite(string role, string operation)
    {
        var admin = await Access();
        var examInput = StaffExamInput();
        if (operation is "review" or "finalize")
        {
            examInput.Questions[0].Type = ElectronicRegisterAPI.Domain.Enums.QuestionType.Open;
            examInput.Questions[0].Options = [];
        }
        var created = await Request("/api/institution/exams", admin, examInput, "POST");
        await Expect(created, HttpStatusCode.Created);
        var exam = await created.Content.ReadFromJsonAsync<Guid>();
        if (operation == "grade")
        {
            var grade = await Request("/api/institution/grades", admin, new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 7, Date = new DateOnly(2026,10,27) }, "POST");
            await Expect(grade, HttpStatusCode.Created);
            var id = await grade.Content.ReadFromJsonAsync<Guid>();
            return new("/api/institution/grades/" + id, role == "staff" ? await StaffToken() : await Access(role),
                new UpdateGradeDto { SubjectId = Id("subject"), Value = 8, Date = new DateOnly(2026,10,27) }, "PUT");
        }
        var input = StaffExamInput();
        if (operation is "update" or "publish")
            return new("/api/institution/exams/" + exam + (operation == "publish" ? "/publish" : ""), role == "staff" ? await StaffToken() : await Access(role),
                operation == "publish" ? null : new UpdateExamDto { ClassSubjectId = input.ClassSubjectId, Description = "Revoked write", AvailableFrom = input.AvailableFrom, AvailableUntil = input.AvailableUntil, DurationMinutes = 30, PassingPercent = 60, Questions = input.Questions }, operation == "publish" ? "POST" : "PUT");
        await Expect(await Request("/api/institution/exams/" + exam + "/publish", admin, method: "POST"), HttpStatusCode.NoContent);
        if (operation == "start")
            return new("/api/institution/exams/" + exam + "/start", await Access("student"), null, "POST");
        var start = await Request("/api/institution/exams/" + exam + "/start", await Access("student"), method: "POST");
        await Expect(start, HttpStatusCode.OK);
        var session = (await start.Content.ReadFromJsonAsync<StartExamDto>())!;
        var detail = (await (await Request("/api/institution/exams/" + exam, admin)).Content.ReadFromJsonAsync<ExamDetailDto>())!;
        var answers = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(detail.Questions, q => new AnswerInputDto { QuestionId = q.Id, OpenText = "Essay", SelectedOptionIds = q.Type == ElectronicRegisterAPI.Domain.Enums.QuestionType.Open ? [] : System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(q.Options, o => o.IsCorrect), o => o.Id)) }));
        if (operation is "review" or "finalize")
            await Expect(await Request(Submission(session.SubmissionId, "submit"), await Access("student"), new SubmitExamDto { ClientRevision = 1, Answers = answers }, "POST"), HttpStatusCode.OK);
        var review = operation is "review" or "finalize" ? (await (await Request(Submission(session.SubmissionId, "review"), admin)).Content.ReadFromJsonAsync<SubmissionReviewDto>())! : null;
        var reviewInput = new ReviewOpenAnswersDto { Answers = review?.Answers.Select(a => new OpenAnswerReviewInputDto { AnswerId = a.AnswerId, AwardedPoints = a.MaxPoints }).ToList() ?? [] };
        if (operation == "finalize")
            await Expect(await Request(Submission(session.SubmissionId, "review"), admin, reviewInput, "PUT"), HttpStatusCode.NoContent);
        object body = operation switch
        {
            "answers" => new SaveExamAnswersDto { ClientRevision = 1, Answers = [] },
            "submit" => new SubmitExamDto { ClientRevision = 1, Answers = answers },
            "review" => reviewInput,
            _ => new FinalizeSubmissionDto()
        };
        return new(Submission(session.SubmissionId, operation), await Access(role), body, operation is "answers" or "review" ? "PUT" : "POST");
    }

    private sealed class QualityOpenBarrier(IInstitutionExamRepositoryFactory inner, TaskCompletionSource<bool> opened, TaskCompletionSource<bool> release) : IInstitutionExamRepositoryFactory
    {
        public IInstitutionExamRepositories Create(ExamPrincipal actor)
        {
            var repositories = inner.Create(actor);
            opened.TrySetResult(true);
            release.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            return repositories;
        }
    }
}
