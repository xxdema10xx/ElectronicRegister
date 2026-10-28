using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionFormativePlanTests
{
    private sealed class CalendarLostResponse:ICalendarFaultObserver
    {
        public Task CheckpointAsync(string name,CancellationToken ct)
        {if(name=="after-commit")throw new System.IO.IOException("Injected response loss after durable commit");return Task.CompletedTask;}
    }
    [Fact] public async Task Calendar_AfterCommitResponseFailureReplaysDurableReceiptOnAnotherHost()
    {
        await using var normal=await CalendarHost();var admin=await Token();var d=await CalendarSetup(normal,admin);var request=CalendarLessonRequest(d.Classes[0],d.Module,d.Teacher);
        await using(var failing=await CalendarHost(s=>s.AddSingleton<ICalendarFaultObserver>(new CalendarLostResponse())))
            Assert.Equal(HttpStatusCode.InternalServerError,(await Send("calendar/lessons",admin,request,"POST",failing)).StatusCode);
        Assert.Equal(1,await Count("institution_lesson"));
        var receipt=await Ok<CalendarReceipt>(await Send("calendar/lessons",admin,request,"POST",normal));Assert.Equal(request.EventId,receipt.EventId);Assert.Equal(1,await Count("institution_lesson"));
    }
    [Fact] public async Task Calendar_StudentCalendarFollowsAuthorizedTransferWithoutRewritingLessons()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);
        var a=await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);var b=await CalendarCreate(h,admin,d.Classes[1],d.Module,d.Teacher,day:6);
        await CalendarPublish(h,admin,a.ClassId);await CalendarPublish(h,admin,b.ClassId);
        await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();await using var cmd=new MySqlCommand("SELECT student_id FROM institution_student_profile WHERE institution_id=@t AND membership_id=@m",c);cmd.Parameters.AddWithValue("@t",tenant);cmd.Parameters.AddWithValue("@m",people["Student"].Member);var student=Guid.Parse(Convert.ToString(await cmd.ExecuteScalarAsync())!);
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")));
        await Sql("UPDATE course_edition SET start_date=@date WHERE institution_id=@t AND id=@e;INSERT INTO student_enrollment(id,institution_id,student_id,course_edition_id,enrollment_date,status) VALUES(@id,@t,@s,@e,@date,'enrolled')",("@date",today.ToDateTime(TimeOnly.MinValue)),("@t",tenant),("@e",edition),("@id",Guid.NewGuid()),("@s",student));
        await Ok<StudentClassTransferResultDto>(await Send($"students/{student}/transfer-class",admin,new{EventId=Guid.NewGuid(),ToClassId=b.ClassId,EffectiveDate=today,Reason="Authorized existing transfer workflow",ExpectedRevision=0},"POST",h));
        var visible=await Ok<CalendarLesson[]>(await Send("calendar/students/me?from=2026-11-01&to=2026-11-30",await Token("Student"),target:h));Assert.Equal(b.Id,Assert.Single(visible).Id);
        Assert.Equal(a.ClassId,(await Ok<CalendarLesson>(await Send("calendar/lessons/"+a.Id,admin,target:h))).ClassId);Assert.Equal(2,await Count("institution_lesson"));
    }
    [Theory][InlineData("Staff")][InlineData("InstitutionAdmin")]
    public async Task Calendar_CombinedOperationalRolesDoNotWidenPersonalStudentScope(string role)
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);
        await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);await CalendarPublish(h,admin,d.Classes[0]);
        await CalendarCreate(h,admin,d.Classes[1],d.Module,d.Teacher,day:6);await CalendarPublish(h,admin,d.Classes[1]);
        var id=Guid.NewGuid();
        await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Student');INSERT INTO student(id,institution_id,first_name,last_name,class_id,class_institution_id) VALUES(@s,@t,'Combined','Student',@c,@t);INSERT INTO institution_student_profile(institution_id,membership_id,student_id) VALUES(@t,@m,@s)",("@m",people[role].Member),("@s",id),("@t",tenant),("@c",d.Classes[1]));
        var token=await Token(role);Assert.Equal(2,(await Ok<CalendarLesson[]>(await Send("calendar?from=2026-11-01&to=2026-11-30",token,target:h))).Length);
        Assert.Equal(d.Classes[1],Assert.Single(await Ok<CalendarLesson[]>(await Send("calendar/students/me?from=2026-11-01&to=2026-11-30",token,target:h))).ClassId);
    }
}
