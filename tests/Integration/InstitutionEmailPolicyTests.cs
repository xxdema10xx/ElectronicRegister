using ElectronicRegisterAPI.Domain.DTOs.Email;
using ElectronicRegisterAPI.Domain.Enums;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    private const string EmailPolicies="/api/institution/email-policies";
    private const string EmailAssignments="/api/institution/email-assignments";
    private EmailTemplate EmailConfig(string local="allievo_{cognome}.{nome:iniziale}", string? domain=null, EmailCollisionStrategy collision=EmailCollisionStrategy.Reject) => new(local,domain??(_a.ToString("N")+".example.test"),new(),collision,"_{n}");
    private Task<Mt3cHosting.ProgramHost> EmailHost(bool enabled=true) => Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,exam:true,
        extraArguments:new[]{"--Multitenancy:InstitutionEmail:Enabled="+enabled});
    private async Task<EmailPolicyVersion> PublishEmail(Mt3cHosting.ProgramHost host,string token,EmailTemplate? config=null,long revision=0,EmailCategory category=EmailCategory.Student)
    {
        var result=await Request(EmailPolicies+"/"+category,token,new EmailPolicyWrite(Guid.NewGuid(),revision,"Published",config??EmailConfig(),"Configured by institution"),revision==0?"POST":"PUT",host);
        await Expect(result,HttpStatusCode.OK);return(await result.Content.ReadFromJsonAsync<EmailPolicyVersion>())!;
    }
    private async Task<EmailAssignment> ReserveEmail(Mt3cHosting.ProgramHost host,string token,Guid student,long revision=1,Guid? eventId=null)
    {
        var response=await Request(EmailAssignments,token,new EmailReserve(eventId??Guid.NewGuid(),EmailCategory.Student,revision,new(student,null,null)),"POST",host);
        await Expect(response,HttpStatusCode.OK);return(await response.Content.ReadFromJsonAsync<EmailAssignment>())!;
    }
    [Fact]
    public async Task EmailPolicy_StaffPreviewAndReservationDoNotCreateMailboxOrChangeGlobalIdentity()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin);
        var staff=await StaffToken();var before=await Digest(true);var ownBefore=await Digest(false);long users=0;await Db(async db=>users=await db.Users.LongCountAsync());
        var preview=await Request(EmailPolicies+"/preview",staff,new EmailPreviewRequest(EmailCategory.Student,1,"Mario","Rossi"),"POST",host);
        await Expect(preview,HttpStatusCode.OK);Assert.Equal("allievo_rossi.m@"+EmailConfig().Domain,(await preview.Content.ReadFromJsonAsync<GeneratedInstitutionEmail>())!.Email);
        var assigned=await ReserveEmail(host,staff,Id("student"));Assert.Equal("reserved",assigned.State);Assert.Equal(Id("student-membership"),assigned.MembershipId);
        Assert.False(assigned.MailboxVerified);Assert.False(assigned.DomainVerified);Assert.False(assigned.LoginEnabled);
        await Db(async db=>{Assert.Equal(users,await db.Users.LongCountAsync());Assert.Equal("student",(await db.Users.AsNoTracking().SingleAsync(u=>u.Id==_teacherUser)).Role);});
        Assert.Equal(before,await Digest(true));
        Assert.Equal(ownBefore,await Digest(false));
        await Expect(await Request(EmailAssignments+"/"+assigned.Id,await Access("student"),host:host),HttpStatusCode.OK);
        await Expect(await Request(EmailAssignments+"/"+assigned.Id,await Access("teacher",true),host:host),HttpStatusCode.NotFound);
    }
    [Theory]
    [InlineData("teacher")][InlineData("student")]
    public async Task EmailPolicy_TeacherAndStudentCannotWritePoliciesOrReserve(string role)
    {
        await using var host=await EmailHost();var token=await Access(role);
        await Expect(await Request(EmailPolicies+"/Student",token,new EmailPolicyWrite(Guid.NewGuid(),0,"Published",EmailConfig(),"Denied"),"POST",host),HttpStatusCode.Forbidden);
        await Expect(await Request(EmailAssignments,token,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student"),null,null)),"POST",host),HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task EmailPolicy_StaffCannotConfigureOverrideOrForceDomain()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin);var staff=await StaffToken();
        await Expect(await Request(EmailPolicies+"/Student",staff,new EmailPolicyWrite(Guid.NewGuid(),1,"Published",EmailConfig(domain:"other.example.it"),"Denied"),"PUT",host),HttpStatusCode.Forbidden);
        await Expect(await Request(EmailPolicies+"/preview",staff,new EmailPreviewRequest(EmailCategory.Student,1,"Mario","Rossi",EmailConfig(domain:"other.example.it")),"POST",host),HttpStatusCode.Forbidden);
        var assigned=await ReserveEmail(host,staff,Id("student"));
        await Expect(await Request(EmailAssignments+"/"+assigned.Id+"/override",staff,new EmailOverride(Guid.NewGuid(),1,1,"other@itsumbria.it","Denied"),"POST",host),HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task EmailPolicy_NewVersionsLeaveOldAddressesAndSnapshotsUnchanged()
    {
        await using var host=await EmailHost();var admin=await Access();var p=await PublishEmail(host,admin);var address=await ReserveEmail(host,admin,Id("student"));
        var next=await PublishEmail(host,admin,EmailConfig("{nome}.{cognome}","studenti.example.it"),1);
        Assert.Equal(p.PolicyId,next.PolicyId);Assert.Equal(2,next.Revision);
        var unchanged=(await(await Request(EmailAssignments+"/"+address.Id,admin,host:host)).Content.ReadFromJsonAsync<EmailAssignment>())!;
        Assert.Equal(address,unchanged);
        await Expect(await Request(EmailAssignments,admin,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student-other"),null,null)),"POST",host),HttpStatusCode.Conflict);
        await Expect(await Request(EmailPolicies+"/Student/history",admin,host:host),HttpStatusCode.OK);
        await Expect(await Request(EmailPolicies+"/Student",admin,new EmailPolicyWrite(Guid.NewGuid(),1,"Published",EmailConfig(),"Stale"),"PUT",host),HttpStatusCode.Conflict);
    }
    [Theory]
    [InlineData(EmailCollisionStrategy.Reject)]
    [InlineData(EmailCollisionStrategy.NumericSuffix)]
    public async Task EmailPolicy_CollisionUsesGlobalUniqueReservationAndConfiguredSuffix(EmailCollisionStrategy strategy)
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin,EmailConfig(collision:strategy));
        var first=await ReserveEmail(host,admin,Id("student"));
        var second=await Request(EmailAssignments,admin,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student-other"),null,null)),"POST",host);
        await Expect(second,strategy==EmailCollisionStrategy.Reject?HttpStatusCode.Conflict:HttpStatusCode.OK);
        if(strategy==EmailCollisionStrategy.NumericSuffix)Assert.Equal(first.Email.Replace("@","_2@"),(await second.Content.ReadFromJsonAsync<EmailAssignment>())!.Email);
    }
    [Fact]
    public async Task EmailPolicy_EventReplayReturnsSameAssignmentAndConflictingReplayNeverWrites()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin);var id=Guid.NewGuid();
        var a=await ReserveEmail(host,admin,Id("student"),eventId:id);var b=await ReserveEmail(host,admin,Id("student"),eventId:id);Assert.Equal(a,b);
        await Expect(await Request(EmailAssignments,admin,new EmailReserve(id,EmailCategory.Student,1,new(Id("student-other"),null,null)),"POST",host),HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task EmailPolicy_OverrideRetainsOldReservationAndAudit()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin);var a=await ReserveEmail(host,admin,Id("student"));
        var changed=await Request(EmailAssignments+"/"+a.Id+"/override",admin,new EmailOverride(Guid.NewGuid(),1,1,"exception@"+EmailConfig().Domain,"Administrative exception"),"POST",host);
        await Expect(changed,HttpStatusCode.OK);var next=(await changed.Content.ReadFromJsonAsync<EmailAssignment>())!;Assert.Equal(2,next.Revision);
        await Expect(await Request(EmailAssignments+"/"+a.Id+"/history",admin,host:host),HttpStatusCode.OK);
        await Expect(await Request(EmailAssignments+"/"+a.Id+"/override",admin,new EmailOverride(Guid.NewGuid(),2,1,a.Email,"Cannot reuse old address"),"POST",host),HttpStatusCode.Conflict);
        await Expect(await Request(EmailAssignments+"/"+a.Id+"/retire",admin,new EmailRetire(Guid.NewGuid(),2,"Retired"),"POST",host),HttpStatusCode.OK);
    }
    [Fact]
    public async Task EmailPolicy_ConcurrentSameAddressHasExactlyOneWinner()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin);
        var a=Request(EmailAssignments,admin,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student"),null,null)),"POST",host);
        var b=Request(EmailAssignments,admin,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student-other"),null,null)),"POST",host);
        var results=await Task.WhenAll(a,b);Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.Conflict));
    }
    [Fact]
    public async Task EmailPolicy_DisabledRolloutAndRevokedStaffCannotMutate()
    {
        await using var active=await EmailHost();var admin=await Access();await PublishEmail(active,admin);var staff=await StaffToken();
        await using var disabled=await EmailHost(false);
        await Expect(await Request(EmailPolicies,staff,host:disabled),HttpStatusCode.Forbidden);
        await Execute("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='Staff'",("@m",Id("teacher-membership")));
        await Expect(await Request(EmailAssignments,staff,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student"),null,null)),"POST",active),HttpStatusCode.Forbidden);
    }
}
