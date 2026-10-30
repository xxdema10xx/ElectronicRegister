using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
using E=ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed partial class InstitutionFormativePlanTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private const string Root="/api/institution/";
    private const string Password="FormativeSynthetic!2026";
    private static readonly string Hash=BCrypt.Net.BCrypt.HashPassword(Password);
    private readonly Guid tenant=Guid.NewGuid(),other=Guid.NewGuid(),path=Guid.NewGuid(),otherPath=Guid.NewGuid(),subject=Guid.NewGuid(),foreignSubject=Guid.NewGuid(),edition=Guid.NewGuid();
    private readonly Dictionary<string,(Guid User,Guid Member)> people=new();
    private Mt3cHosting.ProgramHost host=null!;
    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);
        using var scope=fixture.ScopeFactory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        db.Institutions.AddRange(new E.Institution{Id=tenant,Name="Synthetic formative ITS",Code="F4_"+tenant.ToString("N"),Status="active"},new E.Institution{Id=other,Name="Foreign formative ITS",Code="F4_"+other.ToString("N"),Status="active"});
        await db.SaveChangesAsync();
        void Owned<T>(T value,Guid owner)where T:class {db.Add(value);db.Entry(value).Property<Guid>("InstitutionId").CurrentValue=owner;}
        Owned(new E.StudyPath{Id=path,Name="Synthetic path"},tenant);Owned(new E.StudyPath{Id=otherPath,Name="Foreign path"},other);
        Owned(new E.Subject{Id=subject,Name="Synthetic subject"},tenant);Owned(new E.Subject{Id=foreignSubject,Name="Foreign subject"},other);
        foreach(var role in new[]{"InstitutionAdmin","Staff","Teacher","Student"})
        {
            var user=Guid.NewGuid();var member=Guid.NewGuid();people[role]=(user,member);
            db.Users.Add(new E.User{Id=user,Email=user.ToString("N")+"@formative.example.test",PasswordHash=Hash,Role="student"});
            db.InstitutionMemberships.Add(new E.InstitutionMembership{Id=member,InstitutionId=tenant,UserId=user,Status="active",InstitutionMembershipRoles=new List<E.InstitutionMembershipRole>{new(){MembershipId=member,Role=role}}});
        }
        await db.SaveChangesAsync();
        db.CourseEditions.Add(new E.CourseEdition{Id=edition,InstitutionId=tenant,StudyPathId=path,Code="ED_"+edition.ToString("N"),Name="Synthetic edition",Status="planned",CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow});
        await db.SaveChangesAsync();
        host=await Host();
    }
    // Immutable synthetic audit remains in the disposable DB; never disable retention to clean it up.
    public async Task DisposeAsync(){if(host is not null)await host.DisposeAsync();}
    private Task<Mt3cHosting.ProgramHost> Host(bool enabled=true,Action<IServiceCollection>? configure=null)=>Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,tenant,Array.Empty<Guid>(),enabled:false,school:true,course:true,
        configureServices:configure,extraArguments:new[]{"--Multitenancy:FormativePlans:Enabled="+enabled});
    private async Task<HttpResponseMessage> Send(string route,string? token=null,object? body=null,string method="GET",Mt3cHosting.ProgramHost? target=null)
    {
        using var request=new HttpRequestMessage(new HttpMethod(method),Root+route);
        if(token is not null)request.Headers.Authorization=new("Bearer",token);
        if(body is not null)request.Content=JsonContent.Create(body);
        return await(target??host).Client.SendAsync(request);
    }
    private static async Task<T> Ok<T>(HttpResponseMessage response)
    {Assert.True(response.StatusCode==HttpStatusCode.OK,$"Expected 200, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return(await response.Content.ReadFromJsonAsync<T>())!;}
    private async Task<string> Token(string role="InstitutionAdmin",bool selection=false)
    {
        var person=people[role];var login=await host.Client.PostAsJsonAsync("/api/Auth/identity/login",new LoginDto{Email=person.User.ToString("N")+"@formative.example.test",Password=Password});
        var ticket=await Ok<IdentityTicketDto>(login);if(selection)return ticket.Ticket;
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/Auth/select-institution");request.Headers.Authorization=new("Bearer",ticket.Ticket);request.Content=JsonContent.Create(new SelectInstitutionDto{InstitutionId=tenant,MembershipId=person.Member});
        return(await Ok<InstitutionAccessDto>(await host.Client.SendAsync(request))).Token;
    }
    private async Task Sql(string sql,params (string,object)[] args)
    {await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();SqlLockEvidence.Capture(c);await using var command=new MySqlCommand(sql,c);foreach(var(k,v)in args)command.Parameters.AddWithValue(k,v);await command.ExecuteNonQueryAsync();}
    private async Task<long> Count(string table,string where="",params (string,object)[] args)
    {await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();await using var command=new MySqlCommand($"SELECT COUNT(*) FROM {table} WHERE institution_id=@t "+where,c);command.Parameters.AddWithValue("@t",tenant);foreach(var(k,v)in args)command.Parameters.AddWithValue(k,v);return Convert.ToInt64(await command.ExecuteScalarAsync());}
    private CreateFormativePlan NewPlan(Guid? eventId=null,string code="SYNTH")=>new(eventId??Guid.NewGuid(),path,code,"Synthetic plan",null,"Synthetic fixture creation");
    private ReplaceFormativeDraft Body(long revision=0,int minutes=72000,int theory=72000,Guid? targetSubject=null)=>new(Guid.NewGuid(),revision,"Synthetic version",theory,48000,
        new[]{new FormativeUnitInput(null,"UF","Synthetic unit",null,0,new[]{new FormativeModuleInput(null,"M","Synthetic module",null,minutes,0,targetSubject??subject,
            new[]{new FormativeTopicInput(null,"Design a synthetic model",0)},new[]{new FormativeTopicInput(null,"Synthetic content",0)})})},"Synthetic structure edit");
    private async Task<(string Token,FormativeReceipt Plan,FormativePlanSnapshot Version)> Draft(bool structure=true)
    {
        var token=await Token();var plan=await Ok<FormativeReceipt>(await Send("formative-plans",token,NewPlan(),"POST"));
        var version=await Ok<FormativeReceipt>(await Send($"formative-plans/{plan.PlanId}/versions",token,new CreateFormativeVersion(Guid.NewGuid(),"Synthetic version",72000,48000,"Synthetic draft"),"POST"));
        if(structure)await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{version.VersionId}",token,Body(),"PUT"));
        return(token,plan,await Version(version.VersionId!.Value,token));
    }
    private Task<FormativePlanSnapshot> Version(Guid id,string token)=>ReadVersion(id,token);
    private async Task<FormativePlanSnapshot> ReadVersion(Guid id,string token)=>await Ok<FormativePlanSnapshot>(await Send($"formative-plan-versions/{id}",token));
    private async Task<FormativePlanSnapshot> Published()
    {var d=await Draft();await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,new FormativeCommand(Guid.NewGuid(),d.Version.Revision,"Synthetic publication"),"POST"));return await Version(d.Version.Id,d.Token);}

    [Fact] public async Task CreateIsPersistentAndReplayReturnsOriginalIdentity()
    {
        var token=await Token();var request=NewPlan();var first=await Ok<FormativeReceipt>(await Send("formative-plans",token,request,"POST"));
        Assert.Equal(first,await Ok<FormativeReceipt>(await Send("formative-plans",token,request,"POST")));
        Assert.Equal(1,await Count("formative_plan"));Assert.Equal(1,await Count("formative_plan_event"));
        Assert.Equal(path,(await Ok<FormativePlanInfo>(await Send($"formative-plans/{first.PlanId}",token))).StudyPathId);
        Assert.Equal(HttpStatusCode.Conflict,(await Send("formative-plans",token,request with{Name="Different intent"},"POST")).StatusCode);
    }
    [Fact] public async Task DuplicateCodeInSameTenantConflicts()
    {var token=await Token();await Ok<FormativeReceipt>(await Send("formative-plans",token,NewPlan(),"POST"));Assert.Equal(HttpStatusCode.Conflict,(await Send("formative-plans",token,NewPlan(code:"synth"),"POST")).StatusCode);}
    [Fact] public async Task ForeignPathAndForeignResourceAreIndistinguishableFromMissing()
    {
        var token=await Token();Assert.Equal(HttpStatusCode.NotFound,(await Send("formative-plans",token,NewPlan() with{StudyPathId=otherPath},"POST")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send($"formative-plans/{Guid.NewGuid()}",token)).StatusCode);
    }
    [Fact] public async Task EmptyDraftIsValidToSaveButNotToPublish()
    {
        var d=await Draft(false);var summary=await Ok<FormativeHourSummary>(await Send($"formative-plan-versions/{d.Version.Id}/hour-summary",d.Token));
        Assert.Equal(72000,summary.MissingMinutes);Assert.False(summary.CanPublish);
        Assert.Equal(HttpStatusCode.Conflict,(await Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,new FormativeCommand(Guid.NewGuid(),0,"Incomplete"),"POST")).StatusCode);
    }
    [Theory][InlineData(70200,1800,0)][InlineData(73800,0,1800)]
    public async Task PublicationRejectsDeficitAndExcessWithoutRewritingBudget(int assigned,long missing,long excess)
    {
        var d=await Draft(false);await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(minutes:assigned),"PUT"));
        var summary=await Ok<FormativeHourSummary>(await Send($"formative-plan-versions/{d.Version.Id}/hour-summary",d.Token));
        Assert.Equal(missing,summary.MissingMinutes);Assert.Equal(excess,summary.ExcessMinutes);Assert.Equal(72000,summary.TheoryPlannedMinutes);
        var failure=await Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,new FormativeCommand(Guid.NewGuid(),1,"Incomplete"),"POST");
        Assert.Equal(HttpStatusCode.Conflict,failure.StatusCode);
        var detail=await failure.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(missing,detail.GetProperty("hourSummary").GetProperty("missingMinutes").GetInt64());
        Assert.Equal(excess,detail.GetProperty("hourSummary").GetProperty("excessMinutes").GetInt64());
    }
    [Theory][InlineData(78000,42000)][InlineData(66000,54000)][InlineData(75,0)]
    public async Task ConfigurableBudgetsPublishExactly(int theory,int internship)
    {var d=await Draft(false);await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(minutes:theory,theory:theory) with{InternshipPlannedMinutes=internship},"PUT"));await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,new FormativeCommand(Guid.NewGuid(),1,"Exact total"),"POST"));}
    [Fact] public async Task DeepClonePersistsNewIdsAndLeavesPublishedVersionUnchanged()
    {
        var original=await Published();var token=await Token();var command=new FormativeCommand(Guid.NewGuid(),original.Revision,"Next curriculum");
        var receipt=await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{original.Id}/clone",token,command,"POST"));
        Assert.Equal(receipt,await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{original.Id}/clone",token,command,"POST")));
        var copy=await Version(receipt.VersionId!.Value,token);Assert.Equal(2,copy.VersionNumber);Assert.Equal(FormativeVersionStatus.Draft,copy.Status);
        Assert.NotEqual(original.Units[0].Id,copy.Units[0].Id);Assert.NotEqual(original.Units[0].Modules[0].Id,copy.Units[0].Modules[0].Id);
        Assert.NotEqual(original.Units[0].Modules[0].Competencies[0].Id,copy.Units[0].Modules[0].Competencies[0].Id);
        Assert.NotEqual(original.Units[0].Modules[0].Contents[0].Id,copy.Units[0].Modules[0].Contents[0].Id);
        Assert.Equal(subject,copy.Units[0].Modules[0].SubjectId);
        await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{copy.Id}",token,Body(copy.Revision),"PUT"));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original),System.Text.Json.JsonSerializer.Serialize(await Version(original.Id,token)));
    }
    [Fact] public async Task ConcurrentCloneNumbersAreDistinctAndSequential()
    {var p=await Published();var token=await Token();var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Send($"formative-plan-versions/{p.Id}/clone",token,new FormativeCommand(Guid.NewGuid(),p.Revision,"Concurrent clone"),"POST")));foreach(var r in responses)await Ok<FormativeReceipt>(r);var versions=await Ok<FormativePlanSnapshot[]>(await Send($"formative-plans/{p.PlanId}/versions",token));Assert.Equal(new[]{1,2,3},versions.Select(x=>x.VersionNumber));}
    [Fact] public async Task SameConcurrentCloneEventCreatesOnlyOneVersion()
    {var p=await Published();var token=await Token();var command=new FormativeCommand(Guid.NewGuid(),p.Revision,"Same intention");var responses=await Task.WhenAll(Send($"formative-plan-versions/{p.Id}/clone",token,command,"POST"),Send($"formative-plan-versions/{p.Id}/clone",token,command,"POST"));Assert.Equal(await Ok<FormativeReceipt>(responses[0]),await Ok<FormativeReceipt>(responses[1]));Assert.Equal(2,await Count("formative_plan_version"));}
    [Fact] public async Task ConcurrentDraftEditsHaveOneWinner()
    {var d=await Draft(false);var responses=await Task.WhenAll(Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(),"PUT"),Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(),"PUT"));Assert.Single(responses.Where(x=>x.StatusCode==HttpStatusCode.OK));Assert.Single(responses.Where(x=>x.StatusCode==HttpStatusCode.Conflict));Assert.Equal(1,(await Version(d.Version.Id,d.Token)).Revision);}
    [Fact] public async Task ConcurrentPublicationHasOneWinnerAndReplayIsStable()
    {var d=await Draft();var command=new FormativeCommand(Guid.NewGuid(),1,"Publish once");var responses=await Task.WhenAll(Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,command,"POST"),Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,command,"POST"));Assert.Equal(await Ok<FormativeReceipt>(responses[0]),await Ok<FormativeReceipt>(responses[1]));Assert.Equal(2,(await Version(d.Version.Id,d.Token)).Revision);}
    [Fact] public async Task BindingIsReusableAuditedIdempotentAndCannotBeReplaced()
    {
        var p=await Published();var token=await Token();var command=new BindFormativeVersion(Guid.NewGuid(),p.Id,"Explicit initial binding");
        var receipt=await Ok<FormativeReceipt>(await Send($"course-editions/{edition}/formative-plan",token,command,"POST"));
        Assert.Equal(receipt,await Ok<FormativeReceipt>(await Send($"course-editions/{edition}/formative-plan",token,command,"POST")));
        Assert.Equal(p.Id,(await Ok<FormativePlanSnapshot>(await Send($"course-editions/{edition}/formative-plan",token))).Id);
        Assert.Equal(HttpStatusCode.Conflict,(await Send($"course-editions/{edition}/formative-plan",token,command with{EventId=Guid.NewGuid()},"POST")).StatusCode);
        Assert.Equal(1,await Count("course_edition_formative_plan"));Assert.Equal(1,await Count("formative_plan_event","AND operation='edition.bind'"));
    }
    [Fact] public async Task BindingDraftFailsAndLegacyEditionStillExists()
    {var d=await Draft();Assert.Equal(HttpStatusCode.Conflict,(await Send($"course-editions/{edition}/formative-plan",d.Token,new BindFormativeVersion(Guid.NewGuid(),d.Version.Id,"Draft denied"),"POST")).StatusCode);Assert.Equal(1,await Count("course_edition"));Assert.Equal(0,await Count("course_edition_formative_plan"));}
    [Fact] public async Task TwoDifferentBindingEventsCannotBothWin()
    {var p=await Published();var token=await Token();var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Send($"course-editions/{edition}/formative-plan",token,new BindFormativeVersion(Guid.NewGuid(),p.Id,"Concurrent binding"),"POST")));Assert.Single(responses.Where(x=>x.StatusCode==HttpStatusCode.OK));Assert.Single(responses.Where(x=>x.StatusCode==HttpStatusCode.Conflict));}
    [Theory][InlineData("Staff")][InlineData("Teacher")][InlineData("Student")]
    public async Task NonAdminCannotMutateOrReadHistory(string role)
    {var d=await Draft();var token=await Token(role);Assert.Equal(HttpStatusCode.Forbidden,(await Send("formative-plans",token,NewPlan(code:"DENIED"),"POST")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await Send($"formative-plan-versions/{d.Version.Id}",token,Body(1),"PUT")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await Send($"formative-plans/{d.Plan.PlanId}/history",token)).StatusCode);}
    [Fact] public async Task StaffReadsCatalogButCombinedTeacherRoleDoesNotGrantWrite()
    {var d=await Draft();var staff=people["Staff"];await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher')",("@m",staff.Member));var token=await Token("Staff");await Ok<FormativePlanInfo[]>(await Send("formative-plans",token));await Ok<FormativePlanSnapshot>(await Send($"formative-plan-versions/{d.Version.Id}",token));Assert.Equal(HttpStatusCode.Forbidden,(await Send($"formative-plan-versions/{d.Version.Id}/publish",token,new FormativeCommand(Guid.NewGuid(),1,"Denied"),"POST")).StatusCode);}
    [Theory][InlineData("Teacher")][InlineData("Student")]
    public async Task PersonalRolesCannotEnumerateCatalogOrUnrelatedEdition(string role)
    {await Published();var token=await Token(role);Assert.Equal(HttpStatusCode.Forbidden,(await Send("formative-plans",token)).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await Send($"course-editions/{edition}/formative-plan",token)).StatusCode);}
    [Fact] public async Task SelectionTicketCannotAccessInstitutionalSurface()
    {Assert.Equal(HttpStatusCode.Unauthorized,(await Send("formative-plans",await Token(selection:true))).StatusCode);}
    [Theory][InlineData("suspended")][InlineData("left")]
    public async Task MembershipChangeInvalidatesExistingToken(string status)
    {var token=await Token();await Sql("UPDATE institution_membership SET status=@s WHERE id=@m",("@s",status),("@m",people["InstitutionAdmin"].Member));Assert.Equal(HttpStatusCode.Forbidden,(await Send("formative-plans",token)).StatusCode);}
    [Fact] public async Task DisabledRolloutHasNoRoutes()
    {await using var disabled=await Host(false);Assert.Equal(HttpStatusCode.NotFound,(await Send("formative-plans",await Token(),target:disabled)).StatusCode);}
    [Theory][InlineData("institutionId")][InlineData("revision")][InlineData("publishedAt")][InlineData("actorUserId")][InlineData("status")]
    public async Task UnknownOrServerOwnedFieldsAreRejected(string field)
    {var token=await Token();var body=new Dictionary<string,object?>{{"eventId",Guid.NewGuid()},{"studyPathId",path},{"code","STRICT"},{"name","Strict"},{"reason","Synthetic"},{field,"forbidden"}};Assert.Equal(HttpStatusCode.BadRequest,(await Send("formative-plans",token,body,"POST")).StatusCode);}
    [Theory][InlineData(-1)][InlineData(0)][InlineData(10000001)]
    public async Task InvalidModuleMinutesNeverPersist(int minutes)
    {var d=await Draft(false);Assert.Equal(HttpStatusCode.BadRequest,(await Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(minutes:minutes),"PUT")).StatusCode);Assert.Equal(0,await Count("formative_module"));}
    [Fact] public async Task CrossTenantSubjectRejectedBeforeMutation()
    {var d=await Draft(false);Assert.Equal(HttpStatusCode.BadRequest,(await Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(targetSubject:foreignSubject),"PUT")).StatusCode);Assert.Equal(0,(await Version(d.Version.Id,d.Token)).Revision);}
    [Theory][InlineData("formative_plan_version","name='changed'")][InlineData("formative_unit","name='changed'")][InlineData("formative_module","planned_minutes=75")][InlineData("formative_module_competency","description='changed'")][InlineData("formative_module_content","description='changed'")]
    public async Task PublishedStructureRejectsDirectSqlUpdates(string table,string set)
    {await Published();var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql($"UPDATE {table} SET {set} WHERE institution_id=@t",("@t",tenant)));Assert.Equal(1644,ex.Number);}
    [Theory][InlineData("formative_unit")][InlineData("formative_module")][InlineData("formative_module_competency")][InlineData("formative_module_content")]
    public async Task PublishedStructureRejectsDirectSqlDeletes(string table)
    {await Published();var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql($"DELETE FROM {table} WHERE institution_id=@t",("@t",tenant)));Assert.Equal(1644,ex.Number);}
    [Theory][InlineData("UPDATE formative_plan_event SET reason='changed'")][InlineData("DELETE FROM formative_plan_event")]
    public async Task AuditIsAppendOnlyAtSqlLevel(string sql)
    {await Draft();var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql(sql+" WHERE institution_id=@t",("@t",tenant)));Assert.Equal(1644,ex.Number);}
    [Fact] public async Task SqlCannotMoveModuleOrAssignForeignSubject()
    {var d=await Draft();var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE formative_module SET subject_id=@s WHERE institution_id=@t",("@s",foreignSubject),("@t",tenant)));Assert.Equal(1452,ex.Number);var second=await Ok<FormativeReceipt>(await Send($"formative-plans/{d.Plan.PlanId}/versions",d.Token,new CreateFormativeVersion(Guid.NewGuid(),"Second",72000,48000,"Second draft"),"POST"));ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE formative_module SET version_id=@v WHERE institution_id=@t",("@v",second.VersionId!.Value),("@t",tenant)));Assert.Equal(1644,ex.Number);}
    [Theory][InlineData("after-audit")][InlineData("after-structure")][InlineData("before-commit")]
    public async Task FaultRollsBackDraftAndAuditTogether(string checkpoint)
    {
        var d=await Draft(false);var before=await Count("formative_plan_event");await using var failed=await Host(configure:s=>s.AddSingleton<IFormativePlanFaultObserver>(new FailAt(checkpoint)));
        Assert.Equal(HttpStatusCode.InternalServerError,(await Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(),"PUT",failed)).StatusCode);
        Assert.Equal(before,await Count("formative_plan_event"));Assert.Equal(0,await Count("formative_module"));Assert.Equal(0,(await Version(d.Version.Id,d.Token)).Revision);
    }
    [Theory][InlineData("clone")][InlineData("publish")][InlineData("bind")]
    public async Task FaultAfterAuditRollsBackLifecycleOperation(string operation)
    {
        var d=await Draft();if(operation!="publish")await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,new FormativeCommand(Guid.NewGuid(),1,"Before fault"),"POST"));
        var before=await Count("formative_plan_event");await using var failed=await Host(configure:s=>s.AddSingleton<IFormativePlanFaultObserver>(new FailAt("after-audit")));
        var route=operation=="bind"?$"course-editions/{edition}/formative-plan":$"formative-plan-versions/{d.Version.Id}/{operation}";
        object command=operation=="bind"?new BindFormativeVersion(Guid.NewGuid(),d.Version.Id,"Fault binding"):new FormativeCommand(Guid.NewGuid(),operation=="publish"?1:2,"Fault lifecycle");
        Assert.Equal(HttpStatusCode.InternalServerError,(await Send(route,d.Token,command,"POST",failed)).StatusCode);
        Assert.Equal(before,await Count("formative_plan_event"));Assert.Equal(1,await Count("formative_plan_version"));Assert.Equal(0,await Count("course_edition_formative_plan"));
        Assert.Equal(operation=="publish"?FormativeVersionStatus.Draft:FormativeVersionStatus.Published,(await Version(d.Version.Id,d.Token)).Status);
    }
    private sealed class FailAt(string checkpoint):IFormativePlanFaultObserver
    {public Task CheckpointAsync(string name,CancellationToken ct)=>name==checkpoint?Task.FromException(new Exception("Synthetic fault")):Task.CompletedTask;}

    [Fact] public async Task StudentReadsOnlyOwnEnrollmentPlanAndModule()
    {
        var p=await Published();var admin=await Token();await Ok<FormativeReceipt>(await Send($"course-editions/{edition}/formative-plan",admin,new BindFormativeVersion(Guid.NewGuid(),p.Id,"Personal view fixture"),"POST"));
        var student=Guid.NewGuid();using(var scope=fixture.ScopeFactory.CreateScope())
        {var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();var entity=new E.Student{Id=student,FirstName="Synthetic",LastName="Student"};db.Students.Add(entity);db.Entry(entity).Property<Guid>("InstitutionId").CurrentValue=tenant;await db.SaveChangesAsync();db.InstitutionStudentProfiles.Add(new(){InstitutionId=tenant,MembershipId=people["Student"].Member,StudentId=student});db.StudentEnrollments.Add(new(){Id=Guid.NewGuid(),InstitutionId=tenant,StudentId=student,CourseEditionId=edition,EnrollmentDate=new DateOnly(2026,10,1),Status="enrolled",CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow});await db.SaveChangesAsync();}
        var token=await Token("Student");var response=await Send($"course-editions/{edition}/formative-plan",token);var json=await response.Content.ReadAsStringAsync();Assert.DoesNotContain("actorMembershipId",json);Assert.DoesNotContain("reason",json);Assert.Equal(p.Id,(await Ok<FormativePlanSnapshot>(response)).Id);
        Assert.Equal(p.Units[0].Modules[0].Id,(await Ok<FormativeModuleSnapshot>(await Send($"course-editions/{edition}/formative-plan/modules/{p.Units[0].Modules[0].Id}",token))).Id);
        Assert.Equal(HttpStatusCode.NotFound,(await Send($"course-editions/{edition}/formative-plan/modules/{Guid.NewGuid()}",token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send($"course-editions/{Guid.NewGuid()}/formative-plan",token)).StatusCode);
    }
    [Fact] public async Task TeacherAccessRequiresRealAssignmentToEditionClass()
    {
        var p=await Published();var admin=await Token();await Ok<FormativeReceipt>(await Send($"course-editions/{edition}/formative-plan",admin,new BindFormativeVersion(Guid.NewGuid(),p.Id,"Teacher fixture"),"POST"));
        var teacher=Guid.NewGuid();var assignment=Guid.NewGuid();using(var scope=fixture.ScopeFactory.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            void Add<T>(T row)where T:class{db.Add(row);db.Entry(row).Property<Guid>("InstitutionId").CurrentValue=tenant;}
            var biennium=Guid.NewGuid();var area=Guid.NewGuid();var bsa=Guid.NewGuid();var bsp=Guid.NewGuid();var group=Guid.NewGuid();
            Add(new E.Biennium{Id=biennium,StartYear=new(2500,1,1),EndYear=new(2502,1,1)});Add(new E.StudyArea{Id=area,Name="Synthetic area",Description="Synthetic"});Add(new E.BienniumStudyArea{Id=bsa,BienniumId=biennium,StudyAreaId=area});Add(new E.BienniumStudyPath{Id=bsp,BienniumStudyAreaId=bsa,StudyPathId=path});
            var c=new E.Class{Id=group,BienniumStudyPathId=bsp,Name="Synthetic class"};Add(c);db.Entry(c).Property<Guid?>("CourseEditionId").CurrentValue=edition;
            Add(new E.Teacher{Id=teacher,FirstName="Synthetic",LastName="Teacher"});Add(new E.ClassSubject{Id=assignment,ClassId=group,TeacherId=teacher,SubjectId=subject});await db.SaveChangesAsync();db.InstitutionTeacherProfiles.Add(new(){InstitutionId=tenant,MembershipId=people["Teacher"].Member,TeacherId=teacher});await db.SaveChangesAsync();
        }
        var token=await Token("Teacher");Assert.Equal(p.Id,(await Ok<FormativePlanSnapshot>(await Send($"course-editions/{edition}/formative-plan",token))).Id);
        await Sql("DELETE FROM class_subject WHERE institution_id=@t AND id=@id",("@t",tenant),("@id",assignment));
        Assert.Equal(HttpStatusCode.NotFound,(await Send($"course-editions/{edition}/formative-plan",token)).StatusCode);
    }
    [Fact] public async Task SecurityGenerationAndInstitutionStateAreRevalidated()
    {
        var token=await Token();await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher')",("@m",people["InstitutionAdmin"].Member));Assert.Equal(HttpStatusCode.Forbidden,(await Send("formative-plans",token)).StatusCode);
        token=await Token();await Sql("UPDATE institution SET status='suspended' WHERE id=@t",("@t",tenant));Assert.Equal(HttpStatusCode.Forbidden,(await Send("formative-plans",token)).StatusCode);
    }
    [Fact] public async Task SameCodeInDifferentTenantsIsValidAndForeignIdsAreHidden()
    {
        var d=await Draft();var member=Guid.NewGuid();await Sql("INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES(@m,@t,@u,'active'); INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'InstitutionAdmin')",("@m",member),("@t",other),("@u",people["InstitutionAdmin"].User));
        var ticket=await Token(selection:true);using var request=new HttpRequestMessage(HttpMethod.Post,"/api/Auth/select-institution");request.Headers.Authorization=new("Bearer",ticket);request.Content=JsonContent.Create(new SelectInstitutionDto{InstitutionId=other,MembershipId=member});var token=(await Ok<InstitutionAccessDto>(await host.Client.SendAsync(request))).Token;
        var foreign=await Ok<FormativeReceipt>(await Send("formative-plans",token,NewPlan() with{StudyPathId=otherPath},"POST"));Assert.NotEqual(d.Plan.PlanId,foreign.PlanId);
        Assert.Equal(HttpStatusCode.NotFound,(await Send($"formative-plans/{d.Plan.PlanId}",token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send($"formative-plan-versions/{d.Version.Id}",token,Body(1),"PUT")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send($"course-editions/{edition}/formative-plan",token,new BindFormativeVersion(Guid.NewGuid(),d.Version.Id,"Foreign denied"),"POST")).StatusCode);
    }
    [Fact] public async Task SqlCannotPublishIncompletePlanOrChangeOwnership()
    {
        var d=await Draft(false);var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE formative_plan_version SET status='Published',published_at_utc=UTC_TIMESTAMP(6),revision=revision+1 WHERE institution_id=@t",("@t",tenant)));Assert.Equal(1644,ex.Number);
        ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE formative_plan SET institution_id=@other,revision=revision+1 WHERE institution_id=@t",("@other",other),("@t",tenant)));Assert.Equal(1644,ex.Number);
    }
    [Theory][InlineData("formative_unit","id,institution_id,version_id,code,name,display_order","UUID(),institution_id,version_id,NULL,'Extra',99")]
    [InlineData("formative_module","id,institution_id,version_id,unit_id,name,planned_minutes,display_order","UUID(),institution_id,version_id,unit_id,'Extra',75,99")]
    [InlineData("formative_module_competency","id,institution_id,version_id,module_id,description,display_order","UUID(),institution_id,version_id,module_id,'Extra',99")]
    [InlineData("formative_module_content","id,institution_id,version_id,module_id,description,display_order","UUID(),institution_id,version_id,module_id,'Extra',99")]
    public async Task PublishedStructureRejectsSqlInserts(string table,string columns,string selection)
    {await Published();var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql($"INSERT INTO {table}({columns}) SELECT {selection} FROM {table} WHERE institution_id=@t LIMIT 1",("@t",tenant)));Assert.Equal(1644,ex.Number);}
    [Fact] public async Task PublishedVersionCannotBeEditedByHttp()
    {var p=await Published();var token=await Token();Assert.Equal(HttpStatusCode.Conflict,(await Send($"formative-plan-versions/{p.Id}",token,Body(p.Revision),"PUT")).StatusCode);}
    [Fact] public async Task ReplayAfterLaterRevisionReturnsOriginalReceipt()
    {var d=await Draft(false);var command=Body();var route=$"formative-plan-versions/{d.Version.Id}";var first=await Ok<FormativeReceipt>(await Send(route,d.Token,command,"PUT"));await Ok<FormativeReceipt>(await Send(route,d.Token,Body(1),"PUT"));Assert.Equal(first,await Ok<FormativeReceipt>(await Send(route,d.Token,command,"PUT")));Assert.Equal(2,(await Version(d.Version.Id,d.Token)).Revision);}
    [Fact] public async Task FailedCloneDuringChildCopyLeavesNoNewVersion()
    {var p=await Published();var token=await Token();await using var failed=await Host(configure:s=>s.AddSingleton<IFormativePlanFaultObserver>(new FailAt("after-module")));Assert.Equal(HttpStatusCode.InternalServerError,(await Send($"formative-plan-versions/{p.Id}/clone",token,new FormativeCommand(Guid.NewGuid(),p.Revision,"Fail mid-clone"),"POST",failed)).StatusCode);Assert.Equal(1,await Count("formative_plan_version"));Assert.Equal(1,await Count("formative_module"));}
}
