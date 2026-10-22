#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
using E=ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    private static DateOnly TransferToday=>StudentClassTransferPolicy.OperationalDate(TimeProvider.System,"Europe/Rome");
    private string TransferRoute(Guid? student=null)=>$"/api/institution/students/{student??Id("student")}/transfer-class";
    private string TransferHistory(Guid? student=null)=>$"/api/institution/students/{student??Id("student")}/class-history";
    private TransferStudentClassDto TransferInput(Guid? eventId=null,Guid? to=null,long revision=0,DateOnly? date=null,string reason="Cambio classe richiesto")=>new(){EventId=eventId??Guid.NewGuid(),ToClassId=to??Id("class-other"),ExpectedRevision=revision,EffectiveDate=date??TransferToday,Reason=reason};
    private async Task SeedTransfer(bool cross=false,bool sourceNoEdition=false)
    {
        await Db(async db=>
        {
            foreach(var key in new[]{"source-course","destination-course"})db.CourseEditions.Add(new(){Id=Id(key),InstitutionId=_a,StudyPathId=Id("path"),Code=key,Name=key,StartDate=TransferToday.AddMonths(-1),EndDate=TransferToday.AddYears(1),Status="active"});
            await db.SaveChangesAsync();
            if(!sourceNoEdition)await db.Classes.Where(c=>c.Id==Id("class")).ExecuteUpdateAsync(s=>s.SetProperty(c=>EF.Property<Guid?>(c,"CourseEditionId"),(Guid?)Id("source-course")));
            await db.Classes.Where(c=>c.Id==Id("class-other")).ExecuteUpdateAsync(s=>s.SetProperty(c=>EF.Property<Guid?>(c,"CourseEditionId"),(Guid?)Id(cross?"destination-course":"source-course")));
            db.StudentEnrollments.Add(new(){Id=Id("source-enrollment"),InstitutionId=_a,StudentId=Id("student"),CourseEditionId=Id("source-course"),EnrollmentDate=TransferToday.AddDays(-20),Status="enrolled"});await db.SaveChangesAsync();
        });
    }
    private async Task AssertTransferUnchanged()
    {
        await Db(async db=>{Assert.Equal(Id("class"),(await db.Students.SingleAsync(s=>s.Id==Id("student"))).ClassId);Assert.Equal(0,await db.Students.Where(s=>s.Id==Id("student")).Select(s=>EF.Property<long>(s,"TransferRevision")).SingleAsync());Assert.False(await db.StudentClassTransferEvents.AnyAsync(e=>e.InstitutionId==_a));Assert.Equal("enrolled",(await db.StudentEnrollments.SingleAsync(e=>e.Id==Id("source-enrollment"))).Status);});
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task TransferPreservesCareerAndWritesCoherentEnrollmentHistory(bool cross)
    {
        await SeedTransfer(cross);var token=await Access();var beforeB=await Digest(true);
        await Db(async db=>{db.Grades.Add(new(){Id=Id("historic-grade"),StudentId=Id("student"),ClassSubjectId=Id("assignment"),Value=8,Date=TransferToday.AddDays(-1)});db.Entry(db.Grades.Local.Single()).Property<Guid>("InstitutionId").CurrentValue=_a;await db.SaveChangesAsync();});
        var response=await Request(TransferRoute(),token,TransferInput(),"POST");await Expect(response,HttpStatusCode.OK);
        var receipt=(await response.Content.ReadFromJsonAsync<StudentClassTransferResultDto>())!;Assert.Equal(1,receipt.Revision);Assert.Equal(3,receipt.AppliedLimitMonths);Assert.Equal(Id("class"),receipt.FromClassId);
        await Db(async db=>
        {
            var grade=await db.Grades.SingleAsync(g=>g.Id==Id("historic-grade"));Assert.Equal(Id("student"),grade.StudentId);Assert.Equal(Id("assignment"),grade.ClassSubjectId);Assert.Equal(8,grade.Value);Assert.Equal(TransferToday.AddDays(-1),grade.Date);
            var source=await db.StudentEnrollments.SingleAsync(e=>e.Id==Id("source-enrollment"));Assert.Equal(cross?"withdrawn":"enrolled",source.Status);Assert.Equal(TransferToday.AddDays(-20),source.EnrollmentDate);Assert.Equal(cross?(DateOnly?)TransferToday:null,source.CompletionDate);
            Assert.Equal(cross?2:1,await db.StudentEnrollments.CountAsync(e=>e.StudentId==Id("student")));
            var audit=await db.StudentClassTransferEvents.SingleAsync(e=>e.StudentId==Id("student"));Assert.Equal(_adminUser,audit.ActorUserId);Assert.Equal(Id("admin-membership"),audit.ActorMembershipId);Assert.Equal(Id("class-other"),(await db.Students.SingleAsync(s=>s.Id==Id("student"))).ClassId);
            Assert.Equal(cross?2:0,await db.CourseAdministrationEvents.CountAsync(e=>e.InstitutionId==_a));
            if(cross){var target=await db.StudentEnrollments.SingleAsync(e=>e.CourseEditionId==Id("destination-course"));Assert.Equal(TransferToday,target.EnrollmentDate);Assert.Equal("enrolled",target.Status);}
        });
        var history=await Request(TransferHistory(),token);await Expect(history,HttpStatusCode.OK);var page=(await history.Content.ReadFromJsonAsync<StudentClassTransferPageDto>())!;Assert.Single(page.Items);Assert.Equal(1,page.CurrentRevision);Assert.Equal("Class",page.Items[0].FromClassName);Assert.Equal(beforeB,await Digest(true));
        var gradesResponse=await Request("/api/institution/grades",await Access("student"));await Expect(gradesResponse,HttpStatusCode.OK);var grades=(await gradesResponse.Content.ReadFromJsonAsync<System.Collections.Generic.List<GradeDto>>())!;Assert.Contains(grades,g=>g.Id==Id("historic-grade"));
        var stats=await Request("/api/institution/grades/statistics",await Access("student"));await Expect(stats,HttpStatusCode.OK);var json=(await stats.Content.ReadFromJsonAsync<JsonObject>())!;Assert.Equal(cross?0m:8m,json["yearlyAverage"]!.GetValue<decimal>());
    }
    [Fact]
    public async Task TransferReplaySurvivesLaterTransfersAndPolicyChange()
    {
        await SeedTransfer();var token=await Access();var input=TransferInput();var first=await Request(TransferRoute(),token,input,"POST");await Expect(first,HttpStatusCode.OK);var text=await first.Content.ReadAsStringAsync();
        await Expect(await Request(TransferRoute(),token,TransferInput(to:Id("class"),revision:1),"POST"),HttpStatusCode.OK);
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,course:true,extraArguments:new[]{"--AcademicTransfers:MaxMonthsFromCourseStart=4"});
        var replay=await Request(TransferRoute(),token,input,"POST",host:host);await Expect(replay,HttpStatusCode.OK);Assert.Equal(text,await replay.Content.ReadAsStringAsync());
        var page=(await (await Request(TransferHistory()+"?pageSize=1&pageNumber=2",token)).Content.ReadFromJsonAsync<StudentClassTransferPageDto>())!;Assert.Equal(2,page.TotalCount);Assert.Single(page.Items);Assert.Equal(2,page.Items[0].Receipt.Revision);Assert.Equal(3,page.Items[0].Receipt.AppliedLimitMonths);
        await Expect(await Request(TransferRoute(),token,TransferInput(eventId:input.EventId,reason:"Different"),"POST"),HttpStatusCode.Conflict);
    }
    [Theory][InlineData("teacher")][InlineData("student")]
    public async Task TransferAndHistoryRejectNonAdmin(string role)
    {await SeedTransfer();var token=await Access(role);await Expect(await Request(TransferRoute(),token,TransferInput(),"POST"),HttpStatusCode.Forbidden);await Expect(await Request(TransferHistory(),token),HttpStatusCode.Forbidden);await AssertTransferUnchanged();}
    [Theory][InlineData(true)][InlineData(false)]
    public async Task TransferRejectsMissingSchoolOrCourseRollout(bool school)
    {await SeedTransfer();var token=await Access();await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:school,course:!school);await Expect(await Request(TransferRoute(),token,TransferInput(),"POST",host:host),HttpStatusCode.Forbidden);await AssertTransferUnchanged();}
    [Theory][InlineData("InstitutionId")][InlineData("StudentId")][InlineData("FromClassId")][InlineData("ActorUserId")][InlineData("ActorMembershipId")][InlineData("AppliedLimitMonths")][InlineData("RevisionAfter")]
    public async Task TransferHttpRejectsServerOwnedFields(string field)
    {await SeedTransfer();var body=System.Text.Json.JsonSerializer.SerializeToNode(TransferInput())!.AsObject();body[field]=Guid.NewGuid().ToString();await Expect(await Request(TransferRoute(),await Access(),body,"POST"),HttpStatusCode.BadRequest);await AssertTransferUnchanged();}
    [Theory][InlineData(-1)][InlineData(1)]
    public async Task TransferRejectsBackdatingAndFutureDates(int days)
    {await SeedTransfer();await Expect(await Request(TransferRoute(),await Access(),TransferInput(date:TransferToday.AddDays(days)),"POST"),HttpStatusCode.BadRequest);await AssertTransferUnchanged();}
    [Theory][InlineData("foreign-student")][InlineData("foreign-class")][InlineData("missing-student")][InlineData("missing-class")]
    public async Task TransferForeignIdsAreIndistinguishableFromMissing(string scenario)
    {await SeedTransfer();var token=await Access();var student=scenario=="foreign-student"?Id("student",true):scenario=="missing-student"?Guid.NewGuid():Id("student");var to=scenario=="foreign-class"?Id("class",true):scenario=="missing-class"?Guid.NewGuid():Id("class-other");await Expect(await Request(TransferRoute(student),token,TransferInput(to:to),"POST"),HttpStatusCode.NotFound);await Expect(await Request(TransferHistory(Id("student",true)),token),HttpStatusCode.NotFound);await AssertTransferUnchanged();}
    [Theory][InlineData("same-class")][InlineData("revision")][InlineData("null-class")][InlineData("null-edition")][InlineData("null-start")][InlineData("source-expired")][InlineData("target-expired")][InlineData("completed-course")][InlineData("cancelled-course")][InlineData("missing-enrollment")][InlineData("withdrawn-enrollment")][InlineData("existing-target-enrollment")]
    public async Task TransferAdministrativeConflictsMakeNoMutation(string scenario)
    {
        await SeedTransfer(true,scenario=="null-edition");
        if(scenario=="null-class")await Execute("UPDATE student SET class_id=NULL WHERE institution_id=@o AND id=@s",("@o",_a),("@s",Id("student")));
        if(scenario=="null-start")await Execute("UPDATE course_edition SET start_date=NULL WHERE institution_id=@o AND id=@c",("@o",_a),("@c",Id("source-course")));
        if(scenario.EndsWith("expired"))await Execute("UPDATE course_edition SET start_date=@d WHERE institution_id=@o AND id=@c",("@d",TransferToday.AddMonths(-4)),("@o",_a),("@c",Id(scenario=="source-expired"?"source-course":"destination-course")));
        if(scenario.EndsWith("-course"))await Execute("UPDATE course_edition SET status=@status WHERE institution_id=@o AND id=@c",("@status",scenario.StartsWith("completed")?"completed":"cancelled"),("@o",_a),("@c",Id("destination-course")));
        if(scenario=="missing-enrollment")await Execute("DELETE FROM student_enrollment WHERE institution_id=@o AND id=@e",("@o",_a),("@e",Id("source-enrollment")));
        if(scenario=="withdrawn-enrollment")await Execute("UPDATE student_enrollment SET status='withdrawn',completion_date=@d,revision=revision+1 WHERE institution_id=@o AND id=@e",("@d",TransferToday),("@o",_a),("@e",Id("source-enrollment")));
        if(scenario=="existing-target-enrollment")await Execute("INSERT INTO student_enrollment(id,institution_id,student_id,course_edition_id,enrollment_date,status) VALUES(@e,@o,@s,@c,@d,'enrolled')",("@e",Guid.NewGuid()),("@o",_a),("@s",Id("student")),("@c",Id("destination-course")),("@d",TransferToday));
        var before=await Digest(false);await Expect(await Request(TransferRoute(),await Access(),TransferInput(to:scenario=="same-class"?Id("class"):null,revision:scenario=="revision"?5:0),"POST"),HttpStatusCode.Conflict);Assert.Equal(before,await Digest(false));await Db(async db=>Assert.False(await db.StudentClassTransferEvents.AnyAsync(e=>e.InstitutionId==_a)));
    }
    [Fact]
    public async Task TransferConcurrentIntentionsHaveExactlyOneWinner()
    {await SeedTransfer();var token=await Access();var responses=await Task.WhenAll(Request(TransferRoute(),token,TransferInput(),"POST"),Request(TransferRoute(),token,TransferInput(),"POST"));Assert.Single(responses.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(responses.Where(r=>r.StatusCode==HttpStatusCode.Conflict));await Db(async db=>Assert.Single(await db.StudentClassTransferEvents.Where(e=>e.InstitutionId==_a).ToListAsync()));}
    [Fact]
    public async Task TransferLedgerIsAppendOnlyAndRestrictsHistoricalParentDeletion()
    {await SeedTransfer(true);await Expect(await Request(TransferRoute(),await Access(),TransferInput(),"POST"),HttpStatusCode.OK);await Assert.ThrowsAsync<MySqlException>(()=>Execute("UPDATE student_class_transfer_event SET reason='changed' WHERE institution_id=@o",("@o",_a)));await Assert.ThrowsAsync<MySqlException>(()=>Execute("DELETE FROM student_class_transfer_event WHERE institution_id=@o",("@o",_a)));await Assert.ThrowsAsync<MySqlException>(()=>Execute("DELETE FROM student WHERE institution_id=@o AND id=@s",("@o",_a),("@s",Id("student"))));await Expect(await Request("/api/institution/students/"+Id("student"),await Access(),method:"DELETE"),HttpStatusCode.Conflict);}
    [Fact]
    public async Task TransferRevisionStartsAtZeroWithNoInventedHistoryAndCannotBypassStudentPut()
    {var token=await Access();var page=(await (await Request(TransferHistory(),token)).Content.ReadFromJsonAsync<StudentClassTransferPageDto>())!;Assert.Empty(page.Items);Assert.Equal(0,page.CurrentRevision);await Expect(await Request("/api/institution/students/"+Id("student"),token,new{FirstName="Changed",LastName="Name",ClassId=Id("class-other")},"PUT"),HttpStatusCode.BadRequest);await Expect(await Request(TransferHistory()+"?InstitutionId="+_b,token),HttpStatusCode.BadRequest);}
}
