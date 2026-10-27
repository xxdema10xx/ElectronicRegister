using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Theory]
    [InlineData(EmailCategory.Student)] [InlineData(EmailCategory.Teacher)]
    public async Task EmailPolicy_AtomicCreateSupportsUnlinkedProfilesAndIdempotentReplay(EmailCategory category)
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin,category:category);
        var request=new EmailCreatePerson(Guid.NewGuid(),category,1,"Mario","Rossi");var staff=await StaffToken();
        var response=await Request(EmailAssignments+"/create-person",staff,request,"POST",host);await Expect(response,HttpStatusCode.OK);
        var assigned=(await response.Content.ReadFromJsonAsync<EmailAssignment>())!;Assert.Null(assigned.MembershipId);
        var replay=await Request(EmailAssignments+"/create-person",staff,request,"POST",host);await Expect(replay,HttpStatusCode.OK);
        Assert.Equal(assigned,await replay.Content.ReadFromJsonAsync<EmailAssignment>());
        await Db(async db=>{
            Assert.True(category==EmailCategory.Student?await db.Students.AnyAsync(p=>p.Id==assigned.StudentId):await db.Teachers.AnyAsync(p=>p.Id==assigned.TeacherId));
            var audit=await db.Set<InstitutionEmailEvent>().SingleAsync(e=>e.InstitutionId==_a&&e.Id==request.EventId);
            Assert.Equal(_teacherUser,audit.ActorUserId);Assert.Equal(Id("teacher-membership"),audit.ActorMembershipId);
            Assert.Equal("PersonCreatedAndAssigned",audit.Operation);
        });
    }
    [Fact]
    public async Task EmailPolicy_AtomicCreateRollsBackProfileWhenReservationCollides()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin,EmailConfig("fixed"));
        await ReserveEmail(host,admin,Id("student"));long before=0;await Db(async db=>before=await db.Students.LongCountAsync());
        var eventId=Guid.NewGuid();await Expect(await Request(EmailAssignments+"/create-person",await StaffToken(),new EmailCreatePerson(eventId,EmailCategory.Student,1,"Mario","Rossi"),"POST",host),HttpStatusCode.Conflict);
        await Db(async db=>{Assert.Equal(before,await db.Students.LongCountAsync());Assert.False(await db.Set<InstitutionEmailEvent>().AnyAsync(e=>e.Id==eventId&&e.InstitutionId==_a));});
    }
    [Fact]
    public async Task EmailPolicy_ExistingGlobalEmailConflictsAndGlobalWritersCannotClaimInstitutionalReservations()
    {
        await using var host=await EmailHost();var admin=await Access();var domain=EmailConfig().Domain;
        await Execute("UPDATE user SET email=@email WHERE id=@id",("@email","occupied@"+domain),("@id",_teacherUser));
        await PublishEmail(host,admin,EmailConfig("occupied"));
        await Expect(await Request(EmailAssignments,admin,new EmailReserve(Guid.NewGuid(),EmailCategory.Student,1,new(Id("student"),null,null)),"POST",host),HttpStatusCode.Conflict);
        await PublishEmail(host,admin,EmailConfig("reserved"),1);var address=await ReserveEmail(host,admin,Id("student"),2);
        var update=await Assert.ThrowsAsync<MySqlException>(()=>Execute("UPDATE user SET email=@email WHERE id=@id",("@email",address.Email),("@id",_teacherUser)));Assert.Equal(1062,update.Number);
        await Db(async db=>Assert.Equal("occupied@"+domain,(await db.Users.AsNoTracking().SingleAsync(u=>u.Id==_teacherUser)).Email));
        var insert=await Assert.ThrowsAsync<MySqlException>(()=>Execute("INSERT INTO user(id,email,password_hash,role) VALUES(@id,@email,'not-a-password','student')",("@id",Guid.NewGuid()),("@email",address.Email)));Assert.Equal(1062,insert.Number);
    }
    [Fact]
    public async Task EmailPolicy_UppercaseDisplayUsesLowercaseGlobalNamespace()
    {
        await using var host=await EmailHost();var admin=await Access();var config=EmailConfig("{nome:maiuscolo}.{cognome:maiuscolo}");await PublishEmail(host,admin,config);
        var address=await ReserveEmail(host,admin,Id("student"));Assert.Equal(address.Email.Split('@')[0].ToUpperInvariant(),address.Email.Split('@')[0]);
        await Db(async db=>Assert.True(await db.Set<InstitutionEmailReservation>().AnyAsync(r=>r.Address==address.Email.ToLowerInvariant()&&r.AssignmentId==address.Id)));
    }
    [Fact]
    public async Task EmailPolicy_AppendOnlyAuditAndAssignmentsRejectDirectDestruction()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin);var a=await ReserveEmail(host,admin,Id("student"));
        foreach(var sql in new[]{"DELETE FROM institution_email_assignment WHERE id=@id","UPDATE institution_email_event SET reason='tampered' WHERE assignment_id=@id","DELETE FROM institution_email_event WHERE assignment_id=@id","DELETE FROM institution_email_reservation WHERE assignment_id=@id"})
            Assert.Equal(1644,(await Assert.ThrowsAsync<MySqlException>(()=>Execute(sql,("@id",a.Id)))).Number);
        await Expect(await Request(EmailAssignments+"/"+a.Id,admin,host:host),HttpStatusCode.OK);
    }
    [Fact]
    public async Task EmailPolicy_ConcurrentPolicyVersionsHaveOneWinner()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin);
        var a=Request(EmailPolicies+"/Student",admin,new EmailPolicyWrite(Guid.NewGuid(),1,"Published",EmailConfig("a_{nome}"),"Concurrent A"),"PUT",host);
        var b=Request(EmailPolicies+"/Student",admin,new EmailPolicyWrite(Guid.NewGuid(),1,"Published",EmailConfig("b_{nome}"),"Concurrent B"),"PUT",host);
        var responses=await Task.WhenAll(a,b);Assert.Single(responses.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(responses.Where(r=>r.StatusCode==HttpStatusCode.Conflict));
        await Db(async db=>Assert.Equal(2,await db.Set<InstitutionEmailPolicy>().CountAsync(p=>p.InstitutionId==_a)));
    }
    [Fact]
    public async Task EmailPolicy_MembershipOnlyStaffAddressRequiresAdminAndExplicitNames()
    {
        await using var host=await EmailHost();var admin=await Access();await PublishEmail(host,admin,EmailConfig("staff.{nome}.{cognome}"),category:EmailCategory.Staff);var staff=await StaffToken();
        var request=new EmailReserve(Guid.NewGuid(),EmailCategory.Staff,1,new(null,null,Id("admin-membership"),"Mario","Rossi"));
        await Expect(await Request(EmailAssignments,staff,request,"POST",host),HttpStatusCode.Forbidden);
        var response=await Request(EmailAssignments,admin,request,"POST",host);await Expect(response,HttpStatusCode.OK);
        var assignment=(await response.Content.ReadFromJsonAsync<EmailAssignment>())!;Assert.Null(assignment.StudentId);Assert.Null(assignment.TeacherId);Assert.Equal(Id("admin-membership"),assignment.MembershipId);
    }
}
