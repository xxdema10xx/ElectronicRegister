using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data.Common;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class Mt4bHttpTests
{
    private static object TransitionInput(string status="completed",Guid? eventId=null,long revision=0,string reason="Conclusione amministrativa",string date="2026-10-20") =>
        new {EventId=eventId??Guid.NewGuid(),TargetStatus=status,CompletionDate=date,Reason=reason,ExpectedRevision=revision};
    private async Task<StudentEnrollmentDto> EnrollmentRow(Guid id,string? token=null)
    {var r=await Request(Enrollments+"/"+id,token??await Access());await Expect(r,HttpStatusCode.OK);return (await r.Content.ReadFromJsonAsync<StudentEnrollmentDto>())!;}
    private async Task<List<CourseAdministrationEventDto>> History(Guid id,string? token=null)
    {var r=await Request(Enrollments+"/"+id+"/history",token??await Access());await Expect(r,HttpStatusCode.OK);return (await r.Content.ReadFromJsonAsync<List<CourseAdministrationEventDto>>())!;}

    [Fact]
    public async Task Mt4cSql_AppendOnlyCompositeOwnersAndUtcAreEnforcedByRealMariaDb()
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var row=Assert.Single(await History(id));
        Assert.Equal(_a,row.InstitutionId);Assert.Equal(_adminUser,row.ActorUserId);Assert.Equal(Id("admin-membership"),row.ActorMembershipId);
        Assert.Equal("enrollment.create",row.Action);Assert.Equal(0,row.RevisionAfter);Assert.Null(row.RevisionBefore);
        Assert.Equal(DateTimeKind.Utc,row.OccurredAtUtc.Kind);Assert.InRange(row.OccurredAtUtc,DateTime.UtcNow.AddSeconds(-20),DateTime.UtcNow.AddSeconds(2));
        await Assert.ThrowsAsync<MySqlException>(()=>Execute("UPDATE course_administration_event SET reason='edited' WHERE institution_id=@o AND id=@id",("@o",_a),("@id",row.EventId)));
        await Assert.ThrowsAsync<MySqlException>(()=>Execute("DELETE FROM course_administration_event WHERE institution_id=@o AND id=@id",("@o",_a),("@id",row.EventId)));
        await Assert.ThrowsAsync<MySqlException>(()=>Execute("INSERT INTO course_administration_event(institution_id,id,actor_user_id,actor_membership_id,course_edition_id,action,after_json,occurred_at_utc) VALUES(@a,@id,@u,@m,@e,'course-edition.create','{}',UTC_TIMESTAMP(6))",("@a",_a),("@id",Guid.NewGuid()),("@u",_adminUser),("@m",Id("admin-membership",true)),("@e",a)));
        await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();
        await using var cmd=new MySqlCommand("SELECT COUNT(*) FROM information_schema.key_column_usage WHERE constraint_schema=DATABASE() AND constraint_name LIKE 'fk_mt4c_%' AND ordinal_position=2",c);
        Assert.Equal(5,Convert.ToInt32(await cmd.ExecuteScalarAsync()));
        Assert.Single(await History(id));
    }
    [Theory]
    [InlineData("completed")]
    [InlineData("withdrawn")]
    [InlineData("cancelled")]
    public async Task Mt4cLifecycle_EnrolledTerminatesWithExplicitDateReasonRevisionAndHistory(string state)
    {
        var before=await Digest();var a=await CreateEdition();var id=await CreateEnrollment(a);var token=await Access();var eventId=Guid.NewGuid();
        var response=await Request(Enrollments+"/"+id+"/transition",token,TransitionInput(state,eventId),"POST");await Expect(response,HttpStatusCode.OK);
        var result=(await response.Content.ReadFromJsonAsync<EnrollmentMutationResult>())!;Assert.Equal(eventId,result.EventId);Assert.Equal(1,result.Revision);Assert.Equal(state,result.Status);
        var row=await EnrollmentRow(id);Assert.Equal(1,row.Revision);Assert.Equal(state,row.Status);Assert.Equal(new DateOnly(2026,10,20),row.CompletionDate);
        var events=await History(id);Assert.Equal(2,events.Count);var e=events.Last();Assert.Equal("enrolled",e.PreviousStatus);Assert.Equal(state,e.NewStatus);Assert.Null(e.PreviousCompletionDate);Assert.Equal(row.CompletionDate,e.NewCompletionDate);
        Assert.Equal(0,e.RevisionBefore);Assert.Equal(1,e.RevisionAfter);Assert.Equal("Conclusione amministrativa",e.Reason);Assert.Equal(_adminUser,e.ActorUserId);
        Assert.Equal(before,await Digest());
        await Db(async db=>Assert.Equal("planned",(await db.CourseEditions.AsNoTracking().SingleAsync(x=>x.Id==a)).Status));
        var own=await Rows(Enrollments+"/me",await Access("student"));Assert.Equal(state,Assert.Single(own)!["status"]!.GetValue<string>());
        Assert.Null(own[0]!["reason"]);Assert.Null(own[0]!["actorUserId"]);
    }
    [Theory]
    [InlineData("completed","enrolled")]
    [InlineData("withdrawn","enrolled")]
    [InlineData("cancelled","enrolled")]
    [InlineData("completed","withdrawn")]
    [InlineData("withdrawn","completed")]
    [InlineData("cancelled","completed")]
    public async Task Mt4cLifecycle_TerminalTransitionsAndReentryAreAlwaysRejected(string initial,string target)
    {
        var a=await CreateEdition();var token=await Access();var r=await Request(Enrollments,token,new {StudentId=Id("student"),CourseEditionId=a,EnrollmentDate="2026-10-19",Status=initial},"POST");await Expect(r,HttpStatusCode.Created);var id=await r.Content.ReadFromJsonAsync<Guid>();
        await Expect(await Request(Enrollments+"/"+id+"/transition",token,TransitionInput(target),"POST"),HttpStatusCode.Conflict);
        Assert.Equal(initial,(await EnrollmentRow(id)).Status);Assert.Equal(0,(await EnrollmentRow(id)).Revision);Assert.Single(await History(id));
    }
    [Fact]
    public async Task Mt4cLifecycle_InvalidDatesReasonsMissingRevisionAndImmutableFieldsFailWithoutWrites()
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var token=await Access();
        foreach(var body in new object[]{TransitionInput(date:"2026-10-18"),TransitionInput(reason:" "),TransitionInput(reason:new string('x',1001)),TransitionInput(status:"invented"),
            new {EventId=Guid.NewGuid(),TargetStatus="completed",CompletionDate="2026-10-20",Reason="Missing revision"},
            new {EventId=Guid.NewGuid(),TargetStatus="completed",Reason="Missing date",ExpectedRevision=0},
            new {EventId=Guid.NewGuid(),TargetStatus="completed",CompletionDate="2026-10-20",Reason="Forged",ExpectedRevision=0,InstitutionId=_b}})
            await Expect(await Request(Enrollments+"/"+id+"/transition",token,body,"POST"),HttpStatusCode.BadRequest);
        await Expect(await Request(Enrollments+"/"+id+"/transition",token,TransitionInput(reason:new string('x',20000)),"POST"),HttpStatusCode.RequestEntityTooLarge);
        await Expect(await Request(Enrollments+"/"+id+"/transition",token,TransitionInput(status:"enrolled"),"POST"),HttpStatusCode.Conflict);
        Assert.Equal(0,(await EnrollmentRow(id)).Revision);Assert.Single(await History(id));
    }
    [Fact]
    public async Task Mt4cLifecycle_IdenticalRetryAfterLostResponseIsIdempotentAndChangedIntentConflicts()
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var token=await Access();var eventId=Guid.NewGuid();var body=TransitionInput(eventId:eventId);
        using(var lost=await Request(Enrollments+"/"+id+"/transition",token,body,"POST"))await Expect(lost,HttpStatusCode.OK);
        var retry=await Request(Enrollments+"/"+id+"/transition",token,body,"POST");await Expect(retry,HttpStatusCode.OK);
        Assert.Equal(1,(await retry.Content.ReadFromJsonAsync<EnrollmentMutationResult>())!.Revision);
        await Expect(await Request(Enrollments+"/"+id+"/transition",token,TransitionInput(eventId:eventId,reason:"Different intention"),"POST"),HttpStatusCode.Conflict);
        await Expect(await Request(Enrollments+"/"+id+"/transition",token,TransitionInput(revision:1),"POST"),HttpStatusCode.Conflict);
        Assert.Equal(2,(await History(id)).Count);Assert.Equal(1,(await EnrollmentRow(id)).Revision);
    }
    [Fact]
    public async Task Mt4cLifecycle_ConcurrentSameEventIsIdempotentAndCompetingRevisionsHaveOneWinner()
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var token=await Access();var body=TransitionInput();
        var same=await Task.WhenAll(Request(Enrollments+"/"+id+"/transition",token,body,"POST"),Request(Enrollments+"/"+id+"/transition",token,body,"POST"));
        foreach(var response in same)await Expect(response,HttpStatusCode.OK);Assert.Equal(2,(await History(id)).Count);
        var secondEdition=await CreateEdition(code:"SECOND");var secondId=await CreateEnrollment(secondEdition);
        var login=await Request("/api/Auth/identity/login",body:new LoginDto {Email=_admin2.ToString("N")+"@mt4b.example.test",Password=Password},method:"POST");
        var ticket=(await login.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
        var selected=await Request("/api/Auth/select-institution",ticket,new SelectInstitutionDto {InstitutionId=_a,MembershipId=Id("admin2-membership")},"POST");
        var other=(await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        var competing=await Task.WhenAll(Request(Enrollments+"/"+secondId+"/transition",token,TransitionInput(),"POST"),Request(Enrollments+"/"+secondId+"/transition",other,TransitionInput("withdrawn"),"POST"));
        Assert.Equal(new[]{HttpStatusCode.OK,HttpStatusCode.Conflict}.Order(),competing.Select(r=>r.StatusCode).Order());
        Assert.Equal(1,(await EnrollmentRow(secondId)).Revision);Assert.Equal(2,(await History(secondId)).Count);
    }
    [Fact]
    public async Task Mt4cLifecycle_CorrectionPreservesOldValuesAndLegacyHistoryIsNotFabricated()
    {
        var a=await CreateEdition();var token=await Access();var id=Guid.NewGuid();
        using(var scope=fixture.ScopeFactory.CreateScope()) await scope.ServiceProvider.GetRequiredService<ICourseStructureRepository>().AddEnrollmentAsync(
            new TenantContext(_adminUser,_a,Id("admin-membership"),new[]{InstitutionRole.InstitutionAdmin}),
            new StudentEnrollment(id,_a,Id("student"),a,new DateOnly(2026,10,19),null,"completed"));
        Assert.Empty(await History(id));var before=await EnrollmentRow(id);Assert.Equal(0,before.Revision);
        var eventId=Guid.NewGuid();var body=new {EventId=eventId,CompletionDate="2026-10-21",Reason="Rettifica documentata",ExpectedRevision=0};
        await Expect(await Request(Enrollments+"/"+id+"/completion-date",token,body,"PUT"),HttpStatusCode.OK);
        await Expect(await Request(Enrollments+"/"+id+"/completion-date",token,body,"PUT"),HttpStatusCode.OK);
        var changed=await EnrollmentRow(id);Assert.Equal("completed",changed.Status);Assert.Equal(before.EnrollmentDate,changed.EnrollmentDate);Assert.Equal(before.StudentId,changed.StudentId);Assert.Equal(before.CourseEditionId,changed.CourseEditionId);
        var e=Assert.Single(await History(id));Assert.Equal("enrollment.completion-date.correct",e.Action);Assert.Null(e.PreviousCompletionDate);Assert.Equal(new DateOnly(2026,10,21),e.NewCompletionDate);
        await Expect(await Request(Enrollments+"/"+id+"/completion-date",token,new {EventId=Guid.NewGuid(),CompletionDate="2026-10-22",Reason="Stale",ExpectedRevision=0},"PUT"),HttpStatusCode.Conflict);
        await Expect(await Request(Enrollments+"/"+id+"/completion-date",token,new {EventId=Guid.NewGuid(),CompletionDate="2026-10-22",Reason=" ",ExpectedRevision=1},"PUT"),HttpStatusCode.BadRequest);
        await Expect(await Request(Enrollments+"/"+id+"/completion-date",token,new {EventId=Guid.NewGuid(),CompletionDate="2026-10-22",Reason="Forged",ExpectedRevision=1,EnrollmentDate="2026-10-01"},"PUT"),HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task Mt4cLifecycle_AllMt4bMutationsAreAuditedAtomicallyWithoutDuplicateClassEvents()
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var token=await Access();
        var dto=(await (await Request(Editions+"/"+a,token)).Content.ReadFromJsonAsync<CourseEditionDto>())!;
        await Expect(await Request(Editions+"/"+a,token,new UpdateCourseEditionDto("LOCAL","Audited",null,null,dto.Version),"PUT"),HttpStatusCode.NoContent);
        await Expect(await Request(Editions+"/"+a+"/campuses",token,new {CampusId=Id("campus")},"POST"),HttpStatusCode.Created);
        for(var i=0;i<2;i++)await Expect(await Request(Editions+"/"+a+"/classes/"+Id("class"),token,method:"POST"),HttpStatusCode.NoContent);
        var events=await Rows(Editions+"/"+a+"/audit",token);
        Assert.Equal(new[]{"course-edition.create","enrollment.create","course-edition.update","campus.link","class.assign"}.Order(),events.Select(e=>e!["action"]!.GetValue<string>()).Order());
        Assert.All(events,e=>{Assert.Equal(_a,e!["institutionId"]!.GetValue<Guid>());Assert.Equal(_adminUser,e["actorUserId"]!.GetValue<Guid>());Assert.Equal(Id("admin-membership"),e["actorMembershipId"]!.GetValue<Guid>());});
        Assert.Contains(events,e=>e!["action"]!.GetValue<string>()=="course-edition.update" && e["beforeJson"]!.GetValue<string>().Contains("Edizione") && e["afterJson"]!.GetValue<string>().Contains("Audited"));
    }
    [Theory]
    [InlineData("teacher")]
    [InlineData("student")]
    public async Task Mt4cSecurity_NonAdminsCannotMutateOrReadReasons(string role)
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var token=await Access(role);
        foreach(var (path,method,body) in LifecycleActions(id,a))await Expect(await Request(path,token,body,method),HttpStatusCode.Forbidden);
        Assert.Equal(0,(await EnrollmentRow(id)).Revision);Assert.Single(await History(id));
    }
    private IEnumerable<(string Path,string Method,object? Body)> LifecycleActions(Guid id,Guid edition)
    {
        yield return(Enrollments+"/"+id+"/transition","POST",TransitionInput());
        yield return(Enrollments+"/"+id+"/completion-date","PUT",new {EventId=Guid.NewGuid(),CompletionDate="2026-10-20",Reason="Correction",ExpectedRevision=0});
        yield return(Enrollments+"/"+id+"/history","GET",null);yield return(Editions+"/"+edition+"/audit","GET",null);
    }
    [Fact]
    public async Task Mt4cSecurity_ForeignAndMissingResourcesAreIndistinguishableAndTenantRolesNeverCombine()
    {
        var a=await CreateEdition();var b=await CreateEdition(true);var id=await CreateEnrollment(a);var foreign=await CreateEnrollment(b,true);var token=await Access();
        foreach(var (path,method,body) in LifecycleActions(foreign,b))await Expect(await Request(path,token,body,method),HttpStatusCode.NotFound);
        foreach(var (path,method,body) in LifecycleActions(Guid.NewGuid(),Guid.NewGuid()))await Expect(await Request(path,token,body,method),HttpStatusCode.NotFound);
        await Execute("UPDATE institution_membership_role SET role='Teacher' WHERE membership_id=@id",("@id",Id("admin-membership",true)));
        var scopedB=await Access(b:true);foreach(var (path,method,body) in LifecycleActions(foreign,b))await Expect(await Request(path,scopedB,body,method),HttpStatusCode.Forbidden);
        await Expect(await Request(Enrollments+"/"+id+"/transition",token,TransitionInput(),"POST"),HttpStatusCode.OK);
        await Db(async db=>Assert.Equal(0,await db.StudentEnrollments.AsNoTracking().Where(e=>e.InstitutionId==_b && e.Id==foreign).Select(e=>EF.Property<long>(e,"Revision")).SingleAsync()));
    }
    [Fact]
    public async Task Mt4cLifecycle_CorrectionKeepsThePreviousTerminalDateAndCannotCorrectEnrolled()
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var token=await Access();
        var correction=new {EventId=Guid.NewGuid(),CompletionDate="2026-10-22",Reason="Data corretta",ExpectedRevision=0};
        await Expect(await Request(Enrollments+"/"+id+"/completion-date",token,correction,"PUT"),HttpStatusCode.Conflict);
        await Expect(await Request(Enrollments+"/"+id+"/transition",token,TransitionInput(),"POST"),HttpStatusCode.OK);
        await Expect(await Request(Enrollments+"/"+id+"/completion-date",token,new {EventId=Guid.NewGuid(),CompletionDate="2026-10-22",Reason="Data corretta",ExpectedRevision=1},"PUT"),HttpStatusCode.OK);
        var events=await History(id);Assert.Equal(3,events.Count);Assert.Equal(new DateOnly(2026,10,20),events.Last().PreviousCompletionDate);Assert.Equal(new DateOnly(2026,10,22),events.Last().NewCompletionDate);
        Assert.Equal("completed",events.Last().PreviousStatus);Assert.Equal("completed",events.Last().NewStatus);Assert.Equal(2,(await EnrollmentRow(id)).Revision);
    }
    private sealed class AuditFault(bool after) : DbCommandInterceptor
    {
        private static bool Event(DbCommand c)=>c.CommandText.Contains("INSERT INTO course_administration_event",StringComparison.Ordinal);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c,CommandEventData e,InterceptionResult<int> r,CancellationToken ct=default)
        {if(!after && Event(c))throw new Phase3.InjectedFailure();return base.NonQueryExecutingAsync(c,e,r,ct);}
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand c,CommandExecutedEventData e,int r,CancellationToken ct=default)
        {if(after && Event(c))throw new Phase3.InjectedFailure();return base.NonQueryExecutedAsync(c,e,r,ct);}
    }
    private TenantContext AuditTenant(bool b=false)=>new(_adminUser,b?_b:_a,Id("admin-membership",b),new[]{InstitutionRole.InstitutionAdmin});
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mt4cSql_FailedEventBeforeOrAfterInsertRollsBackStateAndRevision(bool after)
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);
        await using(var provider=Phase3.Provider(fixture,null,new AuditFault(after)))
        using(var scope=provider.CreateScope())
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(()=>scope.ServiceProvider.GetRequiredService<ICourseStructureRepository>().MutateEnrollmentAsync(
                AuditTenant(),id,Guid.NewGuid(),"completed",new DateOnly(2026,10,20),"Fault rollback",0,false,CancellationToken.None));
        var row=await EnrollmentRow(id);Assert.Equal("enrolled",row.Status);Assert.Null(row.CompletionDate);Assert.Equal(0,row.Revision);Assert.Single(await History(id));
    }
    [Theory]
    [InlineData("create-edition")]
    [InlineData("update-edition")]
    [InlineData("campus")]
    [InlineData("class")]
    [InlineData("create-enrollment")]
    public async Task Mt4cSql_EveryMt4bMutationRollsBackWhenItsAuditFails(string operation)
    {
        var a=await CreateEdition();var token=await Access();var dto=(await (await Request(Editions+"/"+a,token)).Content.ReadFromJsonAsync<CourseEditionDto>())!;
        var newId=Guid.NewGuid();
        await using(var provider=Phase3.Provider(fixture,null,new AuditFault(true)))
        using(var scope=provider.CreateScope())
        {
            var repo=scope.ServiceProvider.GetRequiredService<ICourseStructureRepository>();var t=AuditTenant();
            Task Write()=>operation switch {
                "create-edition"=>repo.InsertEditionAsync(t,new CourseEdition(newId,_a,Id("path"),"FAILED","Not persisted",null,null,"planned"),default),
                "update-edition"=>repo.UpdateDescriptionAsync(t,a,new UpdateCourseEditionDto("LOCAL","Not persisted",null,null,dto.Version),default),
                "campus"=>repo.LinkCampusAuditedAsync(t,a,Id("campus"),default),
                "class"=>repo.AssignClassAuditedAsync(t,Id("class"),a,default),
                _=>repo.InsertEnrollmentAsync(t,new StudentEnrollment(newId,_a,Id("student"),a,new DateOnly(2026,10,19),null,"enrolled"),default)};
            await Assert.ThrowsAsync<Phase3.InjectedFailure>(Write);
        }
        await Db(async db=>{
            Assert.False(await db.CourseEditions.AnyAsync(e=>e.Id==newId && e.InstitutionId==_a));
            Assert.False(await db.StudentEnrollments.AnyAsync(e=>e.Id==newId && e.InstitutionId==_a));
            Assert.False(await db.CourseEditionCampuses.AnyAsync(e=>e.CourseEditionId==a && e.InstitutionId==_a));
            Assert.Null(await db.Classes.Where(e=>e.Id==Id("class") && EF.Property<Guid>(e,"InstitutionId")==_a).Select(e=>EF.Property<Guid?>(e,"CourseEditionId")).SingleAsync());
            Assert.Equal("Edizione",(await db.CourseEditions.AsNoTracking().SingleAsync(e=>e.Id==a && e.InstitutionId==_a)).Name);
            Assert.Equal(1,await db.CourseAdministrationEvents.CountAsync(e=>e.InstitutionId==_a));
        });
    }
    [Theory]
    [InlineData("membership")]
    [InlineData("institution")]
    [InlineData("role")]
    public async Task Mt4cSecurity_RevocationAfterJwtBlocksEveryLifecycleAction(string change)
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var token=await Access();
        if(change=="membership")await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id",("@id",Id("admin-membership")));
        else if(change=="institution")await Execute("UPDATE institution SET status='suspended' WHERE id=@id",("@id",_a));
        else await Execute("DELETE FROM institution_membership_role WHERE membership_id=@id",("@id",Id("admin-membership")));
        foreach(var (path,method,body) in LifecycleActions(id,a))await Expect(await Request(path,token,body,method),HttpStatusCode.Forbidden);
        await Db(async db=>{Assert.Equal(0,await db.StudentEnrollments.Where(e=>e.Id==id && e.InstitutionId==_a).Select(e=>EF.Property<long>(e,"Revision")).SingleAsync());Assert.Equal(2,await db.CourseAdministrationEvents.CountAsync(e=>e.InstitutionId==_a));});
    }
    [Fact]
    public async Task Mt4cSecurity_SelectionAnonymousAndClosedFlagsCannotUseNewActions()
    {
        var a=await CreateEdition();var id=await CreateEnrollment(a);var ticket=await Ticket();var token=await Access();
        var legacy=new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer:"mt2-tests",audience:"mt2-client",claims:new[]{new System.Security.Claims.Claim("sub",_adminUser.ToString()),new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role,"admin")},
            expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Mt3cHosting.Key)),"HS256")));
        await using var closed=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,course:false);
        foreach(var (path,method,body) in LifecycleActions(id,a))
        {
            await Expect(await Request(path,ticket,body,method),HttpStatusCode.Unauthorized);
            await Expect(await Request(path,legacy,body,method),HttpStatusCode.Unauthorized);
            await Expect(await Request(path,body:body,method:method),HttpStatusCode.Unauthorized);
            await Expect(await Request(path,token,body,method,closed),HttpStatusCode.Forbidden);
        }
        Assert.Equal(0,(await EnrollmentRow(id)).Revision);
    }
    private sealed class AuditBarrier : DbCommandInterceptor
    {
        private int _count;
        private readonly TaskCompletionSource<bool> _both=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(command.CommandText.Contains("INSERT INTO course_administration_event",StringComparison.Ordinal))
            {
                if(Interlocked.Increment(ref _count)==2)_both.TrySetResult(true);
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);
            }
            return result;
        }
    }
    [Fact]
    public async Task Mt4cSql_DifferentTenantsCanHoldEnrollmentLocksAndWriteEventsConcurrently()
    {
        var a=await CreateEdition();var b=await CreateEdition(true);var aid=await CreateEnrollment(a);var bid=await CreateEnrollment(b,true);var barrier=new AuditBarrier();
        await using var provider=Phase3.Provider(fixture,null,barrier);
        async Task Run(bool other,Guid id)
        {using var scope=provider.CreateScope();await scope.ServiceProvider.GetRequiredService<ICourseStructureRepository>().MutateEnrollmentAsync(AuditTenant(other),id,Guid.NewGuid(),"completed",new DateOnly(2026,10,20),"Parallel A/B",0,false,default);}
        await Task.WhenAll(Run(false,aid),Run(true,bid)).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(1,(await EnrollmentRow(aid)).Revision);Assert.Equal(1,(await EnrollmentRow(bid,await Access(b:true))).Revision);
    }
}
