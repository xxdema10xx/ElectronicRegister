using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ElectronicRegisterAPI.Domain.Enums;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionFormativePlanTests
{
    private static ReplaceFormativeDraft Edit(FormativePlanSnapshot p)=>new(Guid.NewGuid(),p.Revision,p.Name,p.TheoryPlannedMinutes,p.InternshipPlannedMinutes,
        p.Units.Select(u=>new FormativeUnitInput(u.Id,u.Code,u.Name,u.Description,u.DisplayOrder,
            u.Modules.Select(m=>new FormativeModuleInput(m.Id,m.Code,m.Name,m.Description,m.PlannedMinutes,m.DisplayOrder,m.SubjectId,
                m.Competencies.Select(t=>new FormativeTopicInput(t.Id,t.Description,t.DisplayOrder)).ToArray(),m.Contents.Select(t=>new FormativeTopicInput(t.Id,t.Description,t.DisplayOrder)).ToArray())).ToArray())).ToArray(),"Explicit synthetic edit");
    [Fact] public async Task RepeatedSubjectInDifferentUnitsPreservesDistinctIdsAndOrderedOutcomes()
    {
        var d=await Draft(false);var body=Body(minutes:36000);var u=body.Units[0];var m=u.Modules[0] with{Competencies=new[]{new FormativeTopicInput(null,"Second outcome",8),new FormativeTopicInput(null,"First outcome",1)},Contents=new[]{new FormativeTopicInput(null,"Second topic",8),new FormativeTopicInput(null,"First topic",1)}};
        body=body with{Units=new[]{u with{DisplayOrder=9,Modules=new[]{m}},u with{DisplayOrder=1,Code="UF2",Modules=new[]{u.Modules[0]}}}};
        await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{d.Version.Id}",d.Token,body,"PUT"));var p=await Version(d.Version.Id,d.Token);
        Assert.Equal(new[]{1,9},p.Units.Select(x=>x.DisplayOrder));Assert.NotEqual(p.Units[0].Modules[0].Id,p.Units[1].Modules[0].Id);Assert.Equal(subject,p.Units[1].Modules[0].SubjectId);Assert.Equal("First outcome",p.Units[1].Modules[0].Competencies[0].Description);
        var edit=Edit(p);edit=edit with{Units=edit.Units.Select(x=>x with{DisplayOrder=10-x.DisplayOrder}).ToArray()};await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{p.Id}",d.Token,edit,"PUT"));var sorted=await Version(p.Id,d.Token);Assert.Equal(p.Units[1].Id,sorted.Units[0].Id);Assert.Equal(2,await Count("formative_module"));Assert.Equal(0,await Count("class_subject"));
    }
    [Fact] public async Task RemovingDraftElementsIsAuditedAndPreservesRevision()
    {var d=await Draft();await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{d.Version.Id}",d.Token,Edit(d.Version) with{Units=Array.Empty<FormativeUnitInput>()},"PUT"));Assert.Equal(0,await Count("formative_module"));var history=await Ok<FormativeHistory[]>(await Send($"formative-plans/{d.Plan.PlanId}/history",d.Token));Assert.Equal(4,history.Length);Assert.Contains(history,e=>e.Operation=="draft.replace"&&e.RevisionAfter==2&&e.BeforeJson!.Contains("Synthetic module"));}
    [Fact] public async Task PlanRenameDoesNotRewritePublishedSnapshot()
    {var p=await Published();var token=await Token();await Ok<FormativeReceipt>(await Send($"formative-plans/{p.PlanId}",token,new RenameFormativePlan(Guid.NewGuid(),0,"New administrative label",null,"Rename plan"),"PUT"));Assert.Equal(p.Name,(await Version(p.Id,token)).Name);Assert.Equal("New administrative label",(await Ok<FormativePlanInfo>(await Send($"formative-plans/{p.PlanId}",token))).Name);}
    [Fact] public async Task TwoPublishedVersionsBindIndependentlyAndCanBeReused()
    {
        var first=await Published();var token=await Token();await Ok<FormativeReceipt>(await Send($"course-editions/{edition}/formative-plan",token,new BindFormativeVersion(Guid.NewGuid(),first.Id,"First edition"),"POST"));
        var receipt=await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{first.Id}/clone",token,new FormativeCommand(Guid.NewGuid(),first.Revision,"Second snapshot"),"POST"));var copy=await Version(receipt.VersionId!.Value,token);await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{copy.Id}",token,Body(minutes:78000,theory:78000),"PUT"));await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{copy.Id}/publish",token,new FormativeCommand(Guid.NewGuid(),1,"Second exact budget"),"POST"));
        foreach(var id in new[]{Guid.NewGuid(),Guid.NewGuid()}){await Sql("INSERT INTO course_edition(id,institution_id,study_path_id,code,name,status) VALUES(@id,@t,@p,@code,'Synthetic next edition','planned')",("@id",id),("@t",tenant),("@p",path),("@code",id.ToString("N")));await Ok<FormativeReceipt>(await Send($"course-editions/{id}/formative-plan",token,new BindFormativeVersion(Guid.NewGuid(),copy.Id,"Next edition"),"POST"));Assert.Equal(copy.Id,(await Ok<FormativePlanSnapshot>(await Send($"course-editions/{id}/formative-plan",token))).Id);}
        Assert.Equal(first.Id,(await Ok<FormativePlanSnapshot>(await Send($"course-editions/{edition}/formative-plan",token))).Id);Assert.Equal(72000,(await Version(first.Id,token)).TheoryPlannedMinutes);
    }
    [Fact] public async Task BindingDifferentStudyPathFailsInHttpAndSql()
    {
        var p=await Published();var token=await Token();var wrongPath=Guid.NewGuid();var wrongEdition=Guid.NewGuid();await Sql("INSERT INTO study_path(id,institution_id,name) VALUES(@p,@t,'Different path');INSERT INTO course_edition(id,institution_id,study_path_id,code,name,status) VALUES(@id,@t,@p,@code,'Different edition','planned')",("@p",wrongPath),("@t",tenant),("@id",wrongEdition),("@code",wrongEdition.ToString("N")));
        Assert.Equal(HttpStatusCode.Conflict,(await Send($"course-editions/{wrongEdition}/formative-plan",token,new BindFormativeVersion(Guid.NewGuid(),p.Id,"Wrong path"),"POST")).StatusCode);
        var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("INSERT INTO course_edition_formative_plan(id,institution_id,study_path_id,course_edition_id,version_id,created_at_utc) VALUES(@id,@t,@p,@e,@v,UTC_TIMESTAMP(6))",("@id",Guid.NewGuid()),("@t",tenant),("@p",path),("@e",wrongEdition),("@v",p.Id)));Assert.Equal(1644,ex.Number);
    }
    [Fact] public async Task RevocationWaitsForAuthorizedMutationThenOldTokenFails()
    {
        var d=await Draft(false);var barrier=new PauseAt();await using var paused=await Host(configure:s=>s.AddSingleton<IFormativePlanFaultObserver>(barrier));
        var mutation=Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(),"PUT",paused);
        await barrier.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var revoke=Sql("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='InstitutionAdmin'",("@m",people["InstitutionAdmin"].Member));
        try {await Task.Delay(150);Assert.False(revoke.IsCompleted);}finally{barrier.Release.TrySetResult(true);}
        await Ok<FormativeReceipt>(await mutation);await revoke.WaitAsync(TimeSpan.FromSeconds(10));Assert.Equal(HttpStatusCode.Forbidden,(await Send("formative-plans",d.Token)).StatusCode);
    }
    private sealed class PauseAt:IFormativePlanFaultObserver
    {
        public TaskCompletionSource<bool> Acquired {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task CheckpointAsync(string name,CancellationToken ct){if(name=="after-structure"){Acquired.TrySetResult(true);await Release.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);}}
    }
    [Fact] public async Task RuntimeRolloutDisableBeforeCommitRollsBack()
    {
        var d=await Draft(false);var before=await Count("formative_plan_event");var barrier=new PauseAt();await using var paused=await Host(configure:s=>s.AddSingleton<IFormativePlanFaultObserver>(barrier));
        var mutation=Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(),"PUT",paused);await barrier.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        paused.Host.Services.GetRequiredService<IConfiguration>()["Multitenancy:FormativePlans:Enabled"]="false";barrier.Release.TrySetResult(true);
        Assert.Equal(HttpStatusCode.Forbidden,(await mutation).StatusCode);Assert.Equal(before,await Count("formative_plan_event"));Assert.Equal(0,await Count("formative_module"));
    }
    [Fact] public async Task ManagerWithoutCurrentTenantAndRepositoryWithoutAdminFailClosed()
    {
        using var scope=host.Host.Services.CreateScope();var manager=scope.ServiceProvider.GetRequiredService<IInstitutionFormativePlanManager>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>manager.ListAsync(1,20,CancellationToken.None));
        var staff=people["Staff"];var actor=new TenantContext(staff.User,tenant,staff.Member,new[]{InstitutionRole.Staff},0);
        var repository=scope.ServiceProvider.GetRequiredService<IInstitutionFormativePlanRepository>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>repository.CreateAsync(actor,NewPlan(),CancellationToken.None));
    }
    [Fact] public async Task LegacyGlobalAdminDoesNotGrantInstitutionAdmin()
    {
        var student=people["Student"];await Sql("UPDATE user SET role='admin' WHERE id=@id",("@id",student.User));
        Assert.Equal(HttpStatusCode.Forbidden,(await Send("formative-plans",await Token("Student"),NewPlan(),"POST")).StatusCode);
    }
    [Theory][InlineData("PlatformOperator")][InlineData("InstitutionAdmin")]
    public async Task SignedRoleClaimCannotGrantMissingMembershipRole(string role)
    {
        var handler=new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();var token=handler.ReadJwtToken(await Token("Teacher"));
        token.Payload[ElectronicRegisterAPI.Infrastructure.Security.InstitutionTokenContract.Role]=role;
        var header=new System.IdentityModel.Tokens.Jwt.JwtHeader(new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Mt3cHosting.Key)),Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));header["typ"]=token.Header["typ"];
        var signed=handler.WriteToken(new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(header,token.Payload));
        Assert.Contains((await Send("formative-plans",signed,NewPlan(),"POST")).StatusCode,new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden});
    }
    [Fact] public async Task BindingHistoryCannotBeUpdatedOrDeletedBySql()
    {
        var p=await Published();var token=await Token();await Ok<FormativeReceipt>(await Send($"course-editions/{edition}/formative-plan",token,new BindFormativeVersion(Guid.NewGuid(),p.Id,"Initial binding"),"POST"));
        foreach(var sql in new[]{"UPDATE course_edition_formative_plan SET version_id=version_id","DELETE FROM course_edition_formative_plan"}){var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql(sql+" WHERE institution_id=@t",("@t",tenant)));Assert.Equal(1644,ex.Number);}
    }
    [Fact] public async Task TwoDifferentAdminsEditingSameRevisionHaveOneWinner()
    {
        var d=await Draft(false);await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'InstitutionAdmin')",("@m",people["Staff"].Member));var secondAdmin=await Token("Staff");
        var responses=await Task.WhenAll(Send($"formative-plan-versions/{d.Version.Id}",d.Token,Body(),"PUT"),Send($"formative-plan-versions/{d.Version.Id}",secondAdmin,Body(),"PUT"));Assert.Single(responses.Where(x=>x.StatusCode==HttpStatusCode.OK));Assert.Single(responses.Where(x=>x.StatusCode==HttpStatusCode.Conflict));
    }
    [Fact] public async Task TwoDifferentPublicationEventsHaveOneWinner()
    {var d=await Draft();var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,new FormativeCommand(Guid.NewGuid(),1,"Concurrent publication"),"POST")));Assert.Single(responses.Where(x=>x.StatusCode==HttpStatusCode.OK));Assert.Single(responses.Where(x=>x.StatusCode==HttpStatusCode.Conflict));}
    [Theory][InlineData(-1)][InlineData(0)][InlineData(10000001)]
    public async Task SqlRejectsInvalidDraftMinutes(int minutes)
    {await Draft();var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE formative_module SET planned_minutes=@minutes WHERE institution_id=@t",("@minutes",minutes),("@t",tenant)));Assert.Equal(1644,ex.Number);}
    [Fact] public async Task SqlRejectsDuplicateDraftOrderAndPublishedVersionDelete()
    {
        var d=await Draft();var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("INSERT INTO formative_unit(id,institution_id,version_id,name,display_order) VALUES(@id,@t,@v,'Duplicate order',0)",("@id",Guid.NewGuid()),("@t",tenant),("@v",d.Version.Id)));Assert.Equal(1062,ex.Number);
        await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{d.Version.Id}/publish",d.Token,new FormativeCommand(Guid.NewGuid(),1,"Published"),"POST"));ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("DELETE FROM formative_plan_version WHERE institution_id=@t",("@t",tenant)));Assert.Equal(1644,ex.Number);
    }
    [Fact] public async Task AuditRetainsActualActorAndBeforeAfterState()
    {
        var d=await Draft();var events=await Ok<FormativeHistory[]>(await Send($"formative-plans/{d.Plan.PlanId}/history",d.Token));
        Assert.All(events,e=>{Assert.Equal(people["InstitutionAdmin"].User,e.ActorUserId);Assert.Equal(people["InstitutionAdmin"].Member,e.ActorMembershipId);Assert.Contains("Receipt",e.AfterJson);Assert.Contains("State",e.AfterJson);Assert.Equal(DateTimeKind.Utc,e.OccurredAtUtc.Kind);});
        var edit=events.Single(e=>e.Operation=="draft.replace");Assert.Contains("Synthetic unit",edit.AfterJson);Assert.NotNull(edit.BeforeJson);Assert.Equal(0,edit.RevisionBefore);Assert.Equal(1,edit.RevisionAfter);
        var ex=await Assert.ThrowsAsync<MySqlException>(()=>Sql("INSERT INTO formative_plan_event(id,institution_id,plan_id,actor_user_id,actor_membership_id,operation,revision_after,request_hash,after_json,reason,occurred_at_utc) VALUES(@id,@t,@p,@u,@m,'forged',0,REPEAT('A',64),'{}','Forged actor',UTC_TIMESTAMP(6))",("@id",Guid.NewGuid()),("@t",tenant),("@p",d.Plan.PlanId),("@u",people["Teacher"].User),("@m",people["InstitutionAdmin"].Member)));Assert.Equal(1644,ex.Number);
    }
    [Fact] public async Task LegacyBackfillDoesNotRewriteFormativeSnapshots()
    {
        var p=await Published();var token=await Token();var before=System.Text.Json.JsonSerializer.Serialize(p);var count=await Count("formative_plan_event");
        await ElectronicRegisterAPI.Infrastructure.Persistence.Migrations.Mt3aOwnershipMigration.BackfillAsync(fixture.ConnectionString,Guid.Parse("4d543341-0000-4000-8000-000000000001"));
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(await Version(p.Id,token)));Assert.Equal(count,await Count("formative_plan_event"));
    }
}
