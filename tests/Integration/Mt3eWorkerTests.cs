using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.BackgroundServices;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class Mt3dHttpTests
{
    private ExpiredExamSubmissionWorker InstitutionWorker(IServiceProvider? provider = null, string enabled = "true") => new(
        (provider ?? _host.Host.Services).GetRequiredService<IServiceScopeFactory>(), NullLogger<ExpiredExamSubmissionWorker>.Instance,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Multitenancy:InstitutionWorker:Enabled"] = enabled }).Build());

    private async Task Technical(Guid submission, bool b = false, IServiceProvider? provider = null, CancellationToken ct = default)
    {
        using var scope = (provider ?? _host.Host.Services).CreateScope();
        var accessor = scope.ServiceProvider.GetService<ICurrentTenant>();
        if (accessor is not null) Assert.Throws<UnauthorizedAccessException>(() => accessor.Value);
        await scope.ServiceProvider.GetRequiredService<IInstitutionExpiryService>().ProcessAsync(new(submission, b ? _b : _a, DateTime.UtcNow), ct);
        if (accessor is not null) Assert.Throws<UnauthorizedAccessException>(() => accessor.Value);
    }

    private async Task AssertTechnical(Guid id, bool b = false, bool open = false, decimal grade = 1)
    {
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == id);
            Assert.NotNull(s.SubmittedAt); Assert.Equal("time_expired", s.SubmitReason);
            Assert.Equal(open ? "pending_review" : "graded", s.Status);
            Assert.Equal(b ? _b : _a, await db.ExamSubmissions.Where(x => x.Id == id).Select(x => EF.Property<Guid>(x,"InstitutionId")).SingleAsync());
            Assert.Equal(1, await db.ExamSubmissions.CountAsync(x => x.ExamId == s.ExamId && x.StudentId == s.StudentId));
            var audit = Assert.Single(await db.AuditEvents.AsNoTracking().Where(a => a.EntityId == id && a.Action == "submission.submit").ToListAsync());
            Assert.Null(audit.ActorUserId); Assert.Equal("worker:" + id.ToString("N"), audit.RequestId);
            Assert.Contains("institution-worker", audit.AfterJson!); Assert.NotNull(audit.BeforeJson);
            Assert.Equal(b ? _b : _a, await db.AuditEvents.Where(a => a.Id == audit.Id).Select(a => EF.Property<Guid>(a,"InstitutionId")).SingleAsync());
            Assert.Equal("published", (await db.Exams.SingleAsync(e => e.Id == s.ExamId)).Status);
            if(open) { Assert.Null(s.GradeId); Assert.Null(s.Passed); }
            else
            {
                var g = await db.Grades.AsNoTracking().SingleAsync(g => g.Id == s.GradeId);
                Assert.Equal(grade, g.Value); Assert.Equal(grade >= 6, s.Passed);
                Assert.Equal(s.StudentId,g.StudentId);
                Assert.Equal(b ? _b : _a, await db.Grades.Where(x => x.Id == g.Id).Select(x => EF.Property<Guid>(x,"InstitutionId")).SingleAsync());
            }
        });
    }

    [Theory]
    [InlineData(QuestionType.Single)]
    [InlineData(QuestionType.Multiple)]
    [InlineData(QuestionType.Open)]
    public async Task Mt3eWorkerDeliversBothTenantsWithEmptyRequiredAndPreservesAttempt(QuestionType type)
    {
        var a = await Start(await Create(type:type)); var b = await Start(await Create(true,type),true);
        var future = await Start(await Create());
        await Expire(a.SubmissionId); await Expire(b.SubmissionId);
        using(var scope=_host.Host.Services.CreateScope())
        {
            var store=scope.ServiceProvider.GetRequiredService<ISubmissionExpiryStore>();
            var rows=await store.SelectAsync(DateTime.UtcNow,null,50,default);
            Assert.Contains(rows,r=>r.SubmissionId==a.SubmissionId&&r.InstitutionId==_a);
            Assert.Contains(rows,r=>r.SubmissionId==b.SubmissionId&&r.InstitutionId==_b);
            Assert.DoesNotContain(rows,r=>r.SubmissionId==future.SubmissionId);
        }
        var worker=InstitutionWorker(); await worker.ProcessBatchAsync(default);
        await AssertTechnical(a.SubmissionId,open:type==QuestionType.Open); await AssertTechnical(b.SubmissionId,true,type==QuestionType.Open);
        var da=await Digest(false); var db=await Digest(true);
        await worker.ProcessBatchAsync(default); await Technical(a.SubmissionId); await Technical(b.SubmissionId,true);
        Assert.Equal(da,await Digest(false)); Assert.Equal(db,await Digest(true));
        await Db(async db=> {var s=await db.ExamSubmissions.SingleAsync(s=>s.Id==a.SubmissionId);Assert.Equal(a.StartedAt,s.StartedAt);Assert.Equal(0ul,s.LastClientRevision);});
    }

    [Theory]
    [InlineData("false")]
    [InlineData("invalid")]
    [InlineData("")]
    public async Task Mt3eWorkerRolloutDeniesWithoutExplicitTrue(string flag)
    {
        var s=await Start(await Create());await Expire(s.SubmissionId);var before=await Digest(false);
        await InstitutionWorker(enabled:flag).ProcessBatchAsync(default);Assert.Equal(before,await Digest(false));
    }

    [Theory]
    [InlineData("institution")]
    [InlineData("membership")]
    [InlineData("roles")]
    public async Task Mt3eTechnicalExpiryPreservesSavedDataAfterInteractiveRevocation(string revoked)
    {
        var exam=await Create();var s=await Start(exam);var student=await Access("student");
        await Expect(await Request(Submission(s.SubmissionId,"answers"),student,new SaveExamAnswersDto{ClientRevision=7,Answers=Answers(exam)},"PUT"),HttpStatusCode.OK);
        if(revoked=="institution")await Execute("UPDATE institution SET status='suspended' WHERE id=@i",("@i",_a));
        else if(revoked=="membership")await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@i",("@i",Id("student-membership")));
        else await Execute("DELETE FROM institution_membership_role WHERE membership_id=@i",("@i",Id("student-membership")));
        await Expire(s.SubmissionId);await Expect(await Request(Submission(s.SubmissionId,"session"),student),HttpStatusCode.Forbidden);
        await InstitutionWorker().ProcessBatchAsync(default);await AssertTechnical(s.SubmissionId,grade:10);
        await Db(async db=>{var row=await db.ExamSubmissions.SingleAsync(x=>x.Id==s.SubmissionId);Assert.Equal(7ul,row.LastClientRevision);Assert.Single(await db.SubmissionAnswers.Where(a=>a.SubmissionId==row.Id).ToListAsync());});
    }

    [Fact]
    public async Task Mt3eCapabilityCannotBeForgedOrRetargetedAndNoHttpSurfaceExists()
    {
        var a=await Start(await Create());var b=await Start(await Create(true),true);await Expire(a.SubmissionId);await Expire(b.SubmissionId);
        var beforeA=await Digest(false);var beforeB=await Digest(true);
        Assert.Empty(typeof(SubmissionExpiryWork).GetConstructors());
        using(var scope=_host.Host.Services.CreateScope())
        {
            var store=scope.ServiceProvider.GetRequiredService<ISubmissionExpiryStore>();
            Assert.Null(await store.ResolveAsync(a.SubmissionId,_b,default));
            Assert.Null(await store.ResolveAsync(b.SubmissionId,_a,default));
            await Assert.ThrowsAsync<KeyNotFoundException>(()=>scope.ServiceProvider.GetRequiredService<IInstitutionExpiryService>().ProcessAsync(new(a.SubmissionId,_b,DateTime.UtcNow),default));
            var work=(await store.ResolveAsync(a.SubmissionId,_a,default))!;var r=store.Bind(work,default);
            Assert.Null(await r.Submissions.GetByIdAsync(b.SubmissionId,true));Assert.Null(await r.Exams.GetByIdAsync(b.Exam.Id,true));
            Assert.Throws<NotSupportedException>(()=>System.Text.Json.JsonSerializer.Deserialize<SubmissionExpiryWork>("{}"));
        }
        await Expect(await Request("/api/institution/worker",await Access("admin"),method:"POST"),HttpStatusCode.NotFound);
        Assert.Equal(beforeA,await Digest(false));Assert.Equal(beforeB,await Digest(true));
    }

    [Theory]
    [InlineData("worker")]
    [InlineData("http")]
    [InlineData("manual")]
    [InlineData("autosave")]
    public async Task Mt3eWorkerRacesDoNotDuplicateOrAcceptLateAnswers(string competitor)
    {
        var exam=await Create();var s=await Start(exam);var token=await Access("student");await Expire(s.SubmissionId);
        var first=Technical(s.SubmissionId);
        if(competitor=="worker")await Task.WhenAll(first,Technical(s.SubmissionId));
        else
        {
            var suffix=competitor=="http"?"submit-expired":competitor=="manual"?"submit":"answers";
            var method=competitor=="autosave"?"PUT":"POST";
            object? body=competitor=="http"?null:new SubmitExamDto{ClientRevision=99,Answers=Answers(exam)};
            var response=await Request(Submission(s.SubmissionId,suffix),token,body,method);await first;
            Assert.Contains(response.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.Conflict});
        }
        await Db(async db=>{var row=await db.ExamSubmissions.SingleAsync(x=>x.Id==s.SubmissionId);Assert.NotNull(row.GradeId);Assert.Equal(1m,(await db.Grades.SingleAsync(g=>g.Id==row.GradeId)).Value);Assert.Equal(0ul,row.LastClientRevision);Assert.Empty(await db.SubmissionAnswers.Where(a=>a.SubmissionId==row.Id).ToListAsync());Assert.Equal(1,await db.AuditEvents.CountAsync(a=>a.EntityId==row.Id&&a.Action=="submission.submit"));});
    }

    [Fact]
    public async Task Mt3eWorkerAllowsCloseOnlyAfterLastInProgressAndDoesNotPenalizeViolations()
    {
        var exam=await Create();var s=await Start(exam);var teacher=await Access("teacher");var student=await Access("student");
        await Expect(await Request(Submission(s.SubmissionId,"violations"),student,new InstitutionViolationDto{EventId=Guid.NewGuid(),Type="tab_hidden"},"POST"),HttpStatusCode.OK);
        await Expect(await Request(Submission(s.SubmissionId,"answers"),student,new SaveExamAnswersDto{ClientRevision=1,Answers=Answers(exam)},"PUT"),HttpStatusCode.OK);
        await Expire(s.SubmissionId);await Expect(await Request(Exams+"/"+exam.Id+"/close",teacher,method:"POST"),HttpStatusCode.Conflict);
        await Technical(s.SubmissionId);await AssertTechnical(s.SubmissionId,grade:10);
        await Expect(await Request(Exams+"/"+exam.Id+"/close",teacher,method:"POST"),HttpStatusCode.NoContent);
    }

    private sealed class ExpiryFault(string stage, CancellationTokenSource? cancel = null) : SaveChangesInterceptor
    {
        internal bool Fired;
        private void Fail(DbContext? db,bool after)
        {
            if(Fired||db is null)return;
            var grade=db.ChangeTracker.Entries<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade>().Any();
            var audit=db.ChangeTracker.Entries<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.AuditEvent>().Any();
            if(stage=="grade-before"&&!after&&grade||stage=="grade-after"&&after&&grade||stage=="audit-before"&&!after&&audit||stage=="audit-after"&&after&&audit||stage=="cancel"&&after&&grade)
            {Fired=true;if(cancel is not null){cancel.Cancel();cancel.Token.ThrowIfCancellationRequested();}throw new InvalidOperationException("Injected failure");}
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default){Fail(data.Context,false);return ValueTask.FromResult(result);}
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken ct=default){Fail(data.Context,true);return ValueTask.FromResult(result);}
    }

    [Theory]
    [InlineData("grade-before")]
    [InlineData("grade-after")]
    [InlineData("audit-after")]
    [InlineData("audit-before")]
    [InlineData("cancel")]
    public async Task Mt3eRealTransactionRollsBackAndNextAttemptRecovers(string stage)
    {
        var s=await Start(await Create());await Expire(s.SubmissionId);var a=await Digest(false);var b=await Digest(true);
        using var cts=new CancellationTokenSource();var fault=new ExpiryFault(stage,stage=="cancel"?cts:null);
        await using(var provider=Phase3.Services(fixture,null,fault).BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true,ValidateOnBuild=true}))
            await Assert.ThrowsAnyAsync<Exception>(()=>Technical(s.SubmissionId,provider:provider,ct:cts.Token));
        Assert.True(fault.Fired);Assert.Equal(a,await Digest(false));Assert.Equal(b,await Digest(true));
        await Technical(s.SubmissionId);await AssertTechnical(s.SubmissionId);
    }

    [Fact]
    public async Task Mt3eFailedItemDoesNotBlockOtherTenantAndNewSweepRecovers()
    {
        var a=await Start(await Create());var b=await Start(await Create(true),true);await Expire(a.SubmissionId);await Expire(b.SubmissionId);
        var fault=new ExpiryFault("grade-after");
        await using var provider=Phase3.Services(fixture,null,fault).BuildServiceProvider();var worker=InstitutionWorker(provider);
        await worker.ProcessBatchAsync(default);Assert.True(fault.Fired);
        await Db(async db=>Assert.Equal(1,await db.ExamSubmissions.CountAsync(s=>(s.Id==a.SubmissionId||s.Id==b.SubmissionId)&&s.SubmittedAt!=null)));
        await worker.ProcessBatchAsync(default);await worker.ProcessBatchAsync(default);
        await Db(async db=>Assert.Equal(2,await db.ExamSubmissions.CountAsync(s=>(s.Id==a.SubmissionId||s.Id==b.SubmissionId)&&s.SubmittedAt!=null)));
        await AssertTechnical(a.SubmissionId);await AssertTechnical(b.SubmissionId,true);
    }

    [Fact]
    public async Task Mt3eBoundedKeysetScanAdvancesPastFailureAndResetsOnRestart()
    {
        var a=await Start(await Create());var b=await Start(await Create(true),true);await Expire(a.SubmissionId);await Expire(b.SubmissionId);
        using var scope=_host.Host.Services.CreateScope();var store=scope.ServiceProvider.GetRequiredService<ISubmissionExpiryStore>();
        var first=Assert.Single(await store.SelectAsync(DateTime.UtcNow,null,1,default));
        var second=Assert.Single(await store.SelectAsync(DateTime.UtcNow,first,1,default));Assert.NotEqual(first.SubmissionId,second.SubmissionId);
        Assert.Empty(await store.SelectAsync(DateTime.UtcNow,second,1,default));
        Assert.Equal(first,Assert.Single(await store.SelectAsync(DateTime.UtcNow,null,1,default)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>store.SelectAsync(DateTime.UtcNow,null,51,default));
        await InstitutionWorker().ProcessBatchAsync(default);await AssertTechnical(a.SubmissionId);await AssertTechnical(b.SubmissionId,true);
    }

    [Fact]
    public async Task Mt3eRealHostedTimerDeliversOfflineAndStopsCleanly()
    {
        var s=await Start(await Create());await Expire(s.SubmissionId);
        _host.Host.Services.GetRequiredService<IConfiguration>()["Multitenancy:InstitutionWorker:Enabled"]="true";
        var deadline=DateTime.UtcNow.AddSeconds(22);bool delivered=false;
        while(DateTime.UtcNow<deadline&&!delivered){await Task.Delay(200);await Db(async db=>delivered=await db.ExamSubmissions.AnyAsync(x=>x.Id==s.SubmissionId&&x.SubmittedAt!=null));}
        Assert.True(delivered,"Real hosted worker timer did not dispatch");await AssertTechnical(s.SubmissionId);
        // ProgramHost.DisposeAsync verifies ApplicationStopped and awaits actual worker shutdown.
    }

    private sealed class ConnectionFault : DbConnectionInterceptor
    {
        internal bool Fired;
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,ConnectionEventData data,InterceptionResult result,CancellationToken ct=default)
        { if(!Fired){Fired=true;throw new IOException("Injected connection interruption");}return ValueTask.FromResult(result); }
    }
    private sealed class CommandTimeoutFault : DbCommandInterceptor
    {
        internal bool Fired;
        internal readonly System.Diagnostics.Stopwatch Elapsed = new();
        internal Exception? ProviderFailure;
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData data, CancellationToken ct = default)
        {
            if (Fired && command.CommandText.Contains("SLEEP(5)")) ProviderFailure = data.Exception;
            return Task.CompletedTask;
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if (!Fired && command.CommandText.Contains("FOR UPDATE"))
            {
                // Match only the worker's exact owned-parent locking read, including all five parameters.
                var pattern = @"SELECT \* FROM exam_submission WHERE id=(@p\d+) AND id=(@p\d+) AND institution_id=(@p\d+) AND student_id=(@p\d+) AND exam_id=(@p\d+) FOR UPDATE";
                var matches = System.Text.RegularExpressions.Regex.Matches(command.CommandText, pattern);
                var match = Assert.Single(matches.Cast<System.Text.RegularExpressions.Match>());
                Assert.All(match.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1),
                    g => Assert.True(command.Parameters.Contains(g.Value)));
                var delayed = match.Value.Replace(" FOR UPDATE", " AND SLEEP(5)=0 FOR UPDATE");
                command.CommandText = command.CommandText.Remove(match.Index, match.Length).Insert(match.Index, delayed);
                command.CommandTimeout = 1;
                Fired = true;
                Elapsed.Start();
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class GradingFault(ISubmissionGradingService inner) : ISubmissionGradingService
    {
        public ClosedGradingResult GradeClosedQuestions(Exam e,IReadOnlyList<SubmissionAnswer> a)=>throw new InvalidOperationException("Injected grading error");
        public SubmissionEvaluation EvaluateAfterSubmit(Exam e,ClosedGradingResult r)=>inner.EvaluateAfterSubmit(e,r);
        public decimal CalculateOpenScore(Exam e,IReadOnlyList<SubmissionAnswer> a)=>inner.CalculateOpenScore(e,a);
        public decimal ConvertToGrade(decimal total,decimal max,decimal passing)=>inner.ConvertToGrade(total,max,passing);
        public void EnsureReviewPointsValid(Question q,decimal p)=>inner.EnsureReviewPointsValid(q,p);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    [InlineData("grading")]
    public async Task Mt3eConnectionTimeoutAndGradingFailuresRecoverWithoutPartialWrites(string kind)
    {
        var s=await Start(await Create());await Expire(s.SubmissionId);var a=await Digest(false);var b=await Digest(true);
        var connection=new ConnectionFault();var timeout=new CommandTimeoutFault();
        var services=Phase3.Services(fixture,null,kind=="connection"?connection:kind=="timeout"?timeout:new ExpiryFault("unused"));
        if (kind == "timeout")
        {
            // Test-only hard cancellation: MySQL 5.7 can swallow KILL QUERY inside SLEEP.
            // Keep the owned query intact and require the provider's actual timeout error.
            var settings = new MySqlConnector.MySqlConnectionStringBuilder(fixture.ConnectionString) { CancellationTimeout = -1 };
            services.AddDbContext<ElectronicRegisterContext>(options =>
                options.UseMySql(settings.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString)));
        }
        if(kind=="grading")services.AddScoped<ISubmissionGradingService>(_=>new GradingFault(new ElectronicRegisterAPI.Business.Services.SubmissionGradingService()));
        await using(var provider=services.BuildServiceProvider())
        {
            var error=await Assert.ThrowsAnyAsync<Exception>(()=>Technical(s.SubmissionId,provider:provider));
            if(kind=="timeout")
            {
                timeout.Elapsed.Stop();
                Assert.True(timeout.Fired);
                var chain = new List<Exception>();
                for (Exception? current = error; current is not null; current = current.InnerException) chain.Add(current);
                Assert.DoesNotContain(chain, e => e is InvalidCastException);
                // Capture at the command boundary: rollback on a hard-closed connection may
                // replace the original provider error. Never classify that wrapper as a timeout.
                var providerError = Assert.IsType<MySqlConnector.MySqlException>(timeout.ProviderFailure);
                Assert.Equal(MySqlConnector.MySqlErrorCode.CommandTimeoutExpired, providerError.ErrorCode);
                if (!chain.Contains(providerError))
                {
                    Assert.IsType<InvalidOperationException>(error);
                    Assert.Equal("Connection must be Open; current state is Closed", error.Message);
                }
                Assert.True(timeout.Elapsed.Elapsed >= TimeSpan.FromMilliseconds(800), "Server-side delay was not observed");
            }
            if(kind=="connection")Assert.True(connection.Fired);
        }
        Assert.Equal(a,await Digest(false));Assert.Equal(b,await Digest(true));await Technical(s.SubmissionId);await AssertTechnical(s.SubmissionId);
        var recovered = await Digest(false);
        await Technical(s.SubmissionId);await AssertTechnical(s.SubmissionId);
        Assert.Equal(recovered, await Digest(false));Assert.Equal(b, await Digest(true));
    }

    [Fact]
    public async Task Mt3eStaleCapabilityAndIncoherentAssignmentFailClosedUnderLock()
    {
        var s=await Start(await Create());await Expire(s.SubmissionId);
        using var scope=_host.Host.Services.CreateScope();var store=scope.ServiceProvider.GetRequiredService<ISubmissionExpiryStore>();
        var work=(await store.ResolveAsync(s.SubmissionId,_a,default))!;var r=store.Bind(work,default);
        await Execute("DELETE FROM exam_student WHERE exam_id=@i",("@i",s.Exam.Id));
        var before=await Digest(false);
        await scope.ServiceProvider.GetRequiredService<ITransactionManager>().ExecuteAsync(async()=>Assert.Null(await r.Submissions.GetByIdForUpdateAsync(s.SubmissionId,true)));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Technical(s.SubmissionId));
        Assert.DoesNotContain(await store.SelectAsync(DateTime.UtcNow,null,50,default),c=>c.SubmissionId==s.SubmissionId);
        Assert.Equal(before,await Digest(false));
    }

    [Fact]
    public async Task Mt3eWorkerRetryPreservesIndependentRetakeGrades()
    {
        var original=await Create();var first=await Start(original);var student=await Access("student");var teacher=await Access("teacher");
        await Expect(await Request(Submission(first.SubmissionId,"submit"),student,new SubmitExamDto{ClientRevision=1,Answers=Answers(original)},"POST"),HttpStatusCode.OK);
        await Expect(await Request(Exams+"/"+original.Id+"/close",teacher,method:"POST"),HttpStatusCode.NoContent);
        var retake=await Create(retake:original.Id);var second=await Start(retake);await Expire(second.SubmissionId);
        await Technical(second.SubmissionId);await Technical(second.SubmissionId);await AssertTechnical(second.SubmissionId);
        await Db(async db=>{var one=await db.ExamSubmissions.SingleAsync(s=>s.Id==first.SubmissionId);var two=await db.ExamSubmissions.SingleAsync(s=>s.Id==second.SubmissionId);Assert.NotEqual(one.GradeId,two.GradeId);Assert.Equal(10m,(await db.Grades.SingleAsync(g=>g.Id==one.GradeId)).Value);Assert.Equal(1m,(await db.Grades.SingleAsync(g=>g.Id==two.GradeId)).Value);});
    }

    [Theory]
    [InlineData("review")]
    [InlineData("finalize")]
    [InlineData("close")]
    public async Task Mt3eWorkerRacesAdministrativeActionsWithoutPartialOfficialGrade(string action)
    {
        var exam=await Create(type:QuestionType.Open);var s=await Start(exam);var teacher=await Access("teacher");await Expire(s.SubmissionId);
        var work=Technical(s.SubmissionId);
        var response=action=="close"?await Request(Exams+"/"+exam.Id+"/close",teacher,method:"POST"):
            await Request(Submission(s.SubmissionId,action),teacher,action=="review"?new ReviewOpenAnswersDto():new FinalizeSubmissionDto(),action=="review"?"PUT":"POST");
        await work;Assert.Contains(response.StatusCode,new[]{HttpStatusCode.NoContent,HttpStatusCode.Conflict});
        await Db(async db=>{var row=await db.ExamSubmissions.SingleAsync(x=>x.Id==s.SubmissionId);Assert.NotNull(row.SubmittedAt);Assert.Equal(1,await db.AuditEvents.CountAsync(a=>a.EntityId==row.Id&&a.Action=="submission.submit"));if(row.GradeId.HasValue)Assert.Equal(1,await db.Grades.CountAsync(g=>g.Id==row.GradeId));});
    }

    [Fact]
    public async Task Mt3eCancellationAndRestartDoNotLoseExpiredSubmission()
    {
        var s=await Start(await Create());await Expire(s.SubmissionId);var before=await Digest(false);
        using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>InstitutionWorker().ProcessBatchAsync(cts.Token));
        Assert.Equal(before,await Digest(false));await InstitutionWorker().ProcessBatchAsync(default);await AssertTechnical(s.SubmissionId);
    }

    [Fact]
    public async Task Mt3eWorkerFlagDoesNotOpenGlobalAccountOrMembershipAdministration()
    {
        _host.Host.Services.GetRequiredService<IConfiguration>()["Multitenancy:InstitutionWorker:Enabled"]="true";
        var beforeA=await Digest(false);var beforeB=await Digest(true);
        string globalBefore="";
        await Db(async db=>globalBefore=System.Text.Json.JsonSerializer.Serialize(await db.Users.AsNoTracking().Where(u=>u.Id==_studentUser).Select(u=>new{u.Id,u.Email,u.PasswordHash,u.Role,u.StudentId,u.TeacherId}).SingleAsync()));
        var calls=new[]{("GET","/api/Users"),("GET","/api/Users/count"),("GET","/api/Users/"+_studentUser),("PUT","/api/Users/update/"+_studentUser),("PUT","/api/Users/updatepassword/"+_studentUser),("POST","/api/Users"),("DELETE","/api/Users/"+_studentUser),("POST","/api/Auth/register"),("POST","/api/Auth/RegisterForAdmin")};
        foreach(var token in new[]{await Access("admin"),await Access("admin",true),await Ticket()})
        foreach(var (method,path) in calls)
        {
            var response=await Request(path,token,method is "POST" or "PUT"?new{email="attack@example.test",password="NotApplied123!",role="admin"}:null,method);
            Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
        }
        foreach(var path in new[]{"/api/institution/users","/api/institution/memberships","/api/institution/audit"})await Expect(await Request(path,await Access("admin")),HttpStatusCode.NotFound);
        Assert.Equal(beforeA,await Digest(false));Assert.Equal(beforeB,await Digest(true));
        await Db(async db=>Assert.Equal(globalBefore,System.Text.Json.JsonSerializer.Serialize(await db.Users.AsNoTracking().Where(u=>u.Id==_studentUser).Select(u=>new{u.Id,u.Email,u.PasswordHash,u.Role,u.StudentId,u.TeacherId}).SingleAsync())));
    }

    [Fact]
    public async Task Mt3eDispatcherSqlAndWriteScopesAreMeasuredAndBounded()
    {
        var a=await Start(await Create());var b=await Start(await Create(true),true);await Expire(a.SubmissionId);await Expire(b.SubmissionId);
        using var observer=new SqlObserver();using var subscription=System.Diagnostics.DiagnosticListener.AllListeners.Subscribe(observer);
        var timer=System.Diagnostics.Stopwatch.StartNew();await InstitutionWorker().ProcessBatchAsync(default);timer.Stop();
        var commands=observer.Commands.ToArray();
        Assert.Contains(commands,c=>c.Sql.Contains("JOIN institution i")&&c.Sql.Contains("LIMIT")&&c.Sql.Contains("deadline_at"));
        var locks=commands.Where(c=>c.Sql.Contains("FOR UPDATE")&&c.Sql.Contains("exam_submission")).ToArray();Assert.Equal(2,locks.Length);
        Assert.All(locks,c=>Assert.Contains("institution_id",c.Sql));Assert.DoesNotContain(commands,c=>c.Sql.Contains("FROM exam WHERE")&&c.Sql.Contains("FOR UPDATE"));
        Assert.All(commands.Where(c=>c.Sql.Contains("UPDATE `exam_submission`")),c=>Assert.Contains("institution_id",c.Sql));
        var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../../tests/Integration/TestResults/mt3e"));Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(System.IO.Path.Combine(path,"worker-query-observations.json"),System.Text.Json.JsonSerializer.Serialize(new{batchSize=50,items=2,queries=commands.Length,milliseconds=timer.Elapsed.TotalMilliseconds,sql=commands.Select(c=>c.Sql)},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        await AssertTechnical(a.SubmissionId);await AssertTechnical(b.SubmissionId,true);
    }

    private sealed class CrossTenantBarrier : SaveChangesInterceptor
    {
        private readonly HashSet<Guid> _contexts=new();
        private readonly TaskCompletionSource<bool> _both=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken ct=default)
        {
            lock(_contexts){_contexts.Add(data.Context!.ContextId.InstanceId);if(_contexts.Count==2)_both.TrySetResult(true);}
            await _both.Task.WaitAsync(TimeSpan.FromSeconds(10));return result;
        }
    }

    [Fact]
    public async Task Mt3eIndependentTenantWorkersHoldSubmissionLocksConcurrently()
    {
        var a=await Start(await Create());var b=await Start(await Create(true),true);await Expire(a.SubmissionId);await Expire(b.SubmissionId);
        await using var provider=Phase3.Services(fixture,null,new CrossTenantBarrier()).BuildServiceProvider();
        await Task.WhenAll(Technical(a.SubmissionId,provider:provider),Technical(b.SubmissionId,true,provider));
        await AssertTechnical(a.SubmissionId);await AssertTechnical(b.SubmissionId,true);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Mt3eFinalInventoryIncludesEveryRegisteredRouteAndNoTechnicalHttpApi(string environment)
    {
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,exam:true,environment:environment);
        host.Host.Services.GetRequiredService<IConfiguration>()["Multitenancy:InstitutionWorker:Enabled"]="true";
        var endpoints=host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>().ToArray();
        var rows=new List<object>();
        using var scope=host.Host.Services.CreateScope();
        var policies=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>();
        foreach(var endpoint in endpoints)
        {
            var action=endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
            var metadata=endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
            var school=endpoint.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionSchoolEndpointAttribute>() is not null;
            var exam=endpoint.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionExamEndpointAttribute>() is not null;
            var course=endpoint.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionCourseEndpointAttribute>() is not null;
            var identity=endpoint.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionIdentityEndpointAttribute>() is not null;
            var membership=endpoint.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionMembershipAdminEndpointAttribute>() is not null;
            var membershipWrite=endpoint.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionMembershipWriteEndpointAttribute>() is not null;
            var onboarding=action?.ControllerName is "InstitutionInvitation" or "InvitationAcceptance";
            var names=metadata.Select(m=>m.Policy).Where(s=>!string.IsNullOrEmpty(s)).Distinct().ToArray();
            var schemes=new List<string>();foreach(var name in names)schemes.AddRange((await policies.GetPolicyAsync(name!))!.AuthenticationSchemes);
            if(school||exam||course||membership){Assert.Equal(new[]{"InstitutionAccess"},schemes.Distinct());Assert.False(identity);}
            var managers=action?.ControllerTypeInfo.DeclaredConstructors.SelectMany(c=>c.GetParameters()).Select(p=>p.ParameterType).ToArray()??Array.Empty<Type>();
            Assert.DoesNotContain(managers,t=>t==typeof(IInstitutionExpiryService)||t==typeof(ISubmissionExpiryStore));
            var repositories=new HashSet<string>();
            void Dependencies(Type type,int depth)
            {
                if(depth>4)return;
                if(type.Name.Contains("Repository")){repositories.Add(type.Name);return;}
                if(type.Namespace?.StartsWith("ElectronicRegisterAPI")!=true)return;
                var instance=type.IsInterface?scope.ServiceProvider.GetService(type):null;
                foreach(var parameter in (instance?.GetType()??type).GetConstructors().SelectMany(c=>c.GetParameters()))Dependencies(parameter.ParameterType,depth+1);
            }
            foreach(var manager in managers)Dependencies(manager,0);
            foreach(var method in endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods??new[]{"GET"})
                rows.Add(new{method,route=endpoint.RoutePattern.RawText,controller=action?.ControllerName,action=action?.ActionName,
                    scheme=schemes.Count>0?string.Join(",",schemes.Distinct()):metadata.Count>0?"Bearer (legacy default)":"anonymous/gate",
                    policy=names,roles=metadata.Select(m=>m.Roles).Where(r=>!string.IsNullOrEmpty(r)),managers=managers.Select(t=>t.Name),repositories,
                    dataAccess=onboarding?"MT5C owner-bound invitations and verified recipient":membershipWrite?"MT5E transactional IAM writer":membership?"MT5A scoped directory":course?"MT4B scoped course projections/writers":exam?"bound Exam/Submission/Grade/Audit; SQL InstitutionId + assignment/owner":school?"MT3B scoped SQL":identity?"global identity; only verified caller memberships":"legacy global graph or development schema; gated",
                    tenantScope=exam||school||course||membership?"verified TenantContext":identity?"identity selection, not school access":"no institutional school access",
                    rollout=onboarding?(membership?"MembershipAdminApi && MembershipWriteApi && Onboarding":"Onboarding"):membershipWrite?"MembershipAdminApi && MembershipWriteApi":membership?"MembershipAdminApi":course?"SchoolApi && CourseApi":exam?"SchoolApi && ExamApi":school?"SchoolApi":identity?"MT2 verified identity":"legacy allowlist or Development diagnostics",
                    authorizationTest=membershipWrite?"Mt5e real HTTP JWT/IAM/rollback/concurrency":membership?"Mt5a directory JWT/cross-tenant":course?"Mt4b all actions JWT/revocation/cross-tenant":exam?"Mt3d all actions JWT/revocation/cross-tenant":school?"Mt3c all actions JWT/cross-tenant":identity?"Mt2IdentityTests":"Mt2 legacy action matrix; Mt3e global account checks"});
        }
        var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../../tests/Integration/TestResults/mt3e"));Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(System.IO.Path.Combine(path,"routes-"+environment+".json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        await Expect(await host.Client.GetAsync("/openapi/v1.json"),environment=="Development"?HttpStatusCode.OK:HttpStatusCode.NotFound);
        await Assert.Single(host.Host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<ExpiredExamSubmissionWorker>()).ProcessBatchAsync(default);
    }
}
