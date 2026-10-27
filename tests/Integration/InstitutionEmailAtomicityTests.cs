using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Theory]
    [InlineData("policy")][InlineData("assignment")][InlineData("create-person")]
    public async Task EmailPolicy_AuditFaultRollsBackPolicyAssignmentAndNewPerson(string operation)
    {
        await using var host=await EmailHost();var token=await Access();
        if(operation!="policy")await PublishEmail(host,token);
        long people=0;await Db(async db=>people=await db.Students.LongCountAsync());
        var eventId=Guid.NewGuid();var trigger="email_test_fault_"+Guid.NewGuid().ToString("N");
        // Disposable MySQL only: add a targeted fault trigger; retain every business guard.
        await Execute($"CREATE TRIGGER {trigger} AFTER INSERT ON institution_email_event FOR EACH ROW BEGIN IF NEW.institution_id='{_a:D}' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected audit fault'; END IF; END");
        try
        {
            object body=operation=="policy"?new EmailPolicyWrite(eventId,0,"Published",EmailConfig(),"Fault injection"):
                operation=="assignment"?new EmailReserve(eventId,EmailCategory.Student,1,new(Id("student"),null,null)):
                new EmailCreatePerson(eventId,EmailCategory.Student,1,"Mario","Rossi");
            var path=operation=="policy"?EmailPolicies+"/Student":EmailAssignments+(operation=="create-person"?"/create-person":"");
            await Expect(await Request(path,token,body,"POST",host),HttpStatusCode.Conflict);
        }
        finally{await Execute($"DROP TRIGGER {trigger}");}
        await Db(async db=>{
            Assert.Equal(people,await db.Students.LongCountAsync());
            Assert.False(await db.Set<InstitutionEmailAssignment>().AnyAsync(a=>a.InstitutionId==_a));
            Assert.False(await db.Set<InstitutionEmailReservation>().AnyAsync(a=>a.InstitutionId==_a));
            Assert.False(await db.Set<InstitutionEmailEvent>().AnyAsync(e=>e.InstitutionId==_a&&e.Id==eventId));
            Assert.Equal(operation=="policy"?0:1,await db.Set<InstitutionEmailPolicy>().CountAsync(p=>p.InstitutionId==_a));
        });
    }
    [Fact]
    public async Task EmailPolicy_RevocationWaitsForEmailWriteThenOldTokenFails()
    {
        await using var setup=await EmailHost();var admin=await Access();await PublishEmail(setup,admin);var token=await StaffToken();
        var acquired=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,exam:true,membershipAdmin:true,membershipWrite:true,
            extraArguments:new[]{"--Multitenancy:InstitutionEmail:Enabled=true"},configureServices:services=>{
                var descriptor=services.Last(s=>s.ServiceType==typeof(IInstitutionWriteAuthorization));
                services.AddScoped<IInstitutionWriteAuthorization>(sp=>new StaffWriteBarrier((IInstitutionWriteAuthorization)ActivatorUtilities.CreateInstance(sp,descriptor.ImplementationType!),acquired,release));
            });
        long revision=0;await Db(async db=>revision=(await db.InstitutionMembershipSecurityStates.SingleAsync(s=>s.MembershipId==Id("teacher-membership"))).SecurityGeneration);
        var write=Request(EmailAssignments,token,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student"),null,null)),"POST",host);
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var revoke=Request("/api/institution/admin/members/"+Id("teacher-membership")+"/mutations",admin,new{EventId=Guid.NewGuid(),ExpectedRevision=revision,Action="RemoveRole",Role="Staff",Reason="Email concurrent revocation"},"POST",host);
        try{await Task.WhenAny(revoke,Task.Delay(250));Assert.False(revoke.IsCompleted);}
        finally{release.TrySetResult(true);}
        await Expect(await write,HttpStatusCode.OK);await Expect(await revoke,HttpStatusCode.OK);
        await Expect(await Request(EmailAssignments,token,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student-other"),null,null)),"POST",setup),HttpStatusCode.Forbidden);
    }
}
