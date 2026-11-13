using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionFormativePlanTests
{
    [Fact] public async Task Calendar_SqlRejectsSignedPublishedOverlapAndOccupancyTampering()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);
        var a=await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);var b=await CalendarCreate(h,admin,d.Classes[1],d.Module,d.Teacher,day:6);
        await CalendarPublish(h,admin,a.ClassId);await CalendarPublish(h,admin,b.ClassId);
        a=await Ok<CalendarLesson>(await Send("calendar/lessons/"+a.Id,admin,target:h));b=await Ok<CalendarLesson>(await Send("calendar/lessons/"+b.Id,admin,target:h));
        var after=b with{Date=a.Date,StartsAt=a.StartsAt,EndsAt=a.EndsAt,StartUtc=a.StartUtc,EndUtc=a.EndUtc,Revision=b.Revision+1};
        await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();await using var tx=await c.BeginTransactionAsync();var ev=Guid.NewGuid();
        await using(var ledger=new MySqlCommand("INSERT INTO institution_calendar_event(id,institution_id,actor_user_id,actor_membership_id,resource_kind,resource_id,operation,revision_before,revision_after,request_hash,state_json,receipt_json,reason,occurred_at_utc) VALUES(@id,@t,@u,@m,'Lesson',@l,'LessonRescheduled',@before,@after,REPEAT('A',64),@json,'{}','Signed native overlap probe',UTC_TIMESTAMP(6))",c,tx))
        {
            ledger.Parameters.AddWithValue("@id",ev);ledger.Parameters.AddWithValue("@t",tenant);ledger.Parameters.AddWithValue("@u",people["InstitutionAdmin"].User);ledger.Parameters.AddWithValue("@m",people["InstitutionAdmin"].Member);ledger.Parameters.AddWithValue("@l",b.Id);ledger.Parameters.AddWithValue("@before",b.Revision);ledger.Parameters.AddWithValue("@after",after.Revision);ledger.Parameters.AddWithValue("@json",JsonSerializer.Serialize(after));await ledger.ExecuteNonQueryAsync();
        }
        await using(var update=new MySqlCommand("UPDATE institution_lesson SET event_id=@ev,revision=@rev,local_date=@date,start_utc=@start,end_utc=@end WHERE id=@id",c,tx))
        {
            update.Parameters.AddWithValue("@ev",ev);update.Parameters.AddWithValue("@rev",after.Revision);update.Parameters.AddWithValue("@date",after.Date.ToDateTime(TimeOnly.MinValue));update.Parameters.AddWithValue("@start",after.StartUtc);update.Parameters.AddWithValue("@end",after.EndUtc);update.Parameters.AddWithValue("@id",after.Id);
            var failure=await Assert.ThrowsAsync<MySqlException>(()=>update.ExecuteNonQueryAsync());Assert.Equal(1644,failure.Number);
        }
        await tx.RollbackAsync();
        var tamper=await Assert.ThrowsAsync<MySqlException>(()=>Sql("DELETE FROM institution_calendar_occupancy WHERE institution_id=@t",("@t",tenant)));Assert.Equal(1644,tamper.Number);Assert.Equal(4,await Count("institution_calendar_occupancy"));
        var binding=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE class SET course_edition_id=NULL WHERE id=@c",("@c",a.ClassId)));Assert.Equal(1644,binding.Number);
    }
    [Fact] public async Task Calendar_SubstituteReadsOwnPublishedModuleWithoutClassSubjectPrivileges()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);var l=await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);await CalendarPublish(h,admin,l.ClassId);
        await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher');INSERT INTO institution_teacher_profile(institution_id,membership_id,teacher_id) VALUES(@t,@m,@teacher)",("@m",people["Staff"].Member),("@t",tenant),("@teacher",d.Substitute));
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h));await Ok<CalendarReceipt>(await Send($"calendar/lessons/{l.Id}/substitute",admin,new CalendarSubstituteCommand(Guid.NewGuid(),l.Revision,d.Substitute,"Cover"),"POST",h));
        var module=await Ok<FormativeModuleSnapshot>(await Send($"calendar/lessons/{l.Id}/module",await Token("Staff"),target:h));Assert.Equal(d.Module,module.Id);Assert.NotEmpty(module.Competencies);
    }
    private sealed class CalendarFutureClock : TimeProvider { public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UtcNow.AddHours(2); }
    [Fact] public async Task Calendar_ExpiredPreviewCannotCommitAndLatePublicationDoesNotCertifyDelivery()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);
        var p=await CalendarUpload(h,admin,new[]{d.Classes[0]},$"Data;OraInizio;OraFine;Classe;Modulo;Docente\n2026-11-05;09:15;11:45;{d.Classes[0]};{d.Module};{d.Teacher}");
        await using var future=await CalendarHost(s=>s.AddSingleton<TimeProvider>(new CalendarFutureClock()));
        Assert.Equal(HttpStatusCode.Conflict,(await Send($"calendar/imports/{p.Id}/confirm",admin,new CalendarConfirmImport(Guid.NewGuid(),0,p.Digest,true,"Expired"),"POST",future)).StatusCode);
        var r=CalendarLessonRequest(d.Classes[0],d.Module,d.Teacher);r=r with{Lesson=r.Lesson with{Date=new(2020,1,5)}};
        await Ok<CalendarReceipt>(await Send("calendar/lessons",admin,r,"POST",h));
        var period=await Ok<CalendarPeriod>(await Send($"calendar/months/2020/1/classes/{d.Classes[0]}",admin,target:h));Assert.True(period.Approval.IsOverdue);
        await Ok<CalendarReceipt>(await Send($"calendar/months/2020/1/classes/{d.Classes[0]}/publish",admin,new CalendarCommand(Guid.NewGuid(),period.Revision,"Explicit late publication"),"POST",h));
        period=await Ok<CalendarPeriod>(await Send($"calendar/months/2020/1/classes/{d.Classes[0]}",admin,target:h));Assert.True(period.Approval.IsLate);Assert.NotNull(period.PublishedByMembershipId);
        Assert.Equal(0,(await Ok<CalendarHoursReport>(await Send($"calendar/hours?classId={d.Classes[0]}",admin,target:h))).DeliveredMinutes);
    }
    [Fact] public async Task Calendar_TeacherInTwoInstitutionsCannotLeakLessonsAcrossSelectedContexts()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);var l=await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);await CalendarPublish(h,admin,l.ClassId);
        var member=Guid.NewGuid();var teacher=Guid.NewGuid();
        await Sql("INSERT INTO teacher(id,institution_id,first_name,last_name) VALUES(@teacher,@t,'Other','Teacher');INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES(@m,@t,@u,'active');INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher');INSERT INTO institution_teacher_profile(institution_id,membership_id,teacher_id) VALUES(@t,@m,@teacher)",("@teacher",teacher),("@t",other),("@m",member),("@u",people["Teacher"].User));
        var selection=await Token("Teacher",true);using var request=new HttpRequestMessage(HttpMethod.Post,"/api/Auth/select-institution"){Content=JsonContent.Create(new SelectInstitutionDto{InstitutionId=other,MembershipId=member})};request.Headers.Authorization=new("Bearer",selection);
        var token=(await Ok<InstitutionAccessDto>(await h.Client.SendAsync(request))).Token;
        Assert.Empty(await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",token,target:h)));
        Assert.Equal(HttpStatusCode.NotFound,(await Send("calendar/lessons/"+l.Id,token,target:h)).StatusCode);
        Assert.Single(await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",await Token("Teacher"),target:h)));
    }
    [Fact] public async Task Calendar_TeacherStudentCombinationRetainsStudentScopeWithoutInventingTeacherProfile()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);var l=await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);await CalendarPublish(h,admin,l.ClassId);
        await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher')",("@m",people["Student"].Member));var token=await Token("Student");
        Assert.Single(await Ok<CalendarLesson[]>(await Send("calendar/students/me?from=2026-11-01&to=2026-11-30",token,target:h)));
        Assert.Empty(await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",token,target:h)));
        Assert.Equal(HttpStatusCode.Forbidden,(await Send($"calendar/lessons/{l.Id}/complete",token,new CalendarDeliveredCommand(Guid.NewGuid(),1,150,"No teacher identity"),"POST",h)).StatusCode);
    }
}
