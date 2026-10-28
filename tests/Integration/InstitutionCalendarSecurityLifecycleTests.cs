using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionFormativePlanTests
{
    private async Task<CalendarLesson> CalendarCreate(Mt3cHosting.ProgramHost h,string token,Guid c,Guid m,Guid t,int day=5)
    {
        var receipt=await Ok<CalendarReceipt>(await Send("calendar/lessons",token,CalendarLessonRequest(c,m,t,day:day),"POST",h));
        return await Ok<CalendarLesson>(await Send("calendar/lessons/"+receipt.ResourceId,token,target:h));
    }
    private async Task CalendarPublish(Mt3cHosting.ProgramHost h,string token,Guid c)
    {
        var p=await Ok<CalendarPeriod>(await Send($"calendar/months/2026/11/classes/{c}",token,target:h));
        await Ok<CalendarReceipt>(await Send($"calendar/months/2026/11/classes/{c}/publish",token,new CalendarCommand(Guid.NewGuid(),p.Revision,"Publish calendar"),"POST",h));
    }
    [Theory]
    [InlineData("Teacher","create")][InlineData("Teacher","publish")][InlineData("Teacher","hours")]
    [InlineData("Student","create")][InlineData("Student","publish")][InlineData("Student","history")]
    public async Task Calendar_PersonalRolesCannotUseOperationalCapabilities(string role,string op)
    {
        await using var h=await CalendarHost();var admin=await Token();var data=await CalendarSetup(h,admin);var token=await Token(role);
        var lesson=await CalendarCreate(h,admin,data.Classes[0],data.Module,data.Teacher);
        var response=op switch
        {
            "create"=>await Send("calendar/lessons",token,CalendarLessonRequest(data.Classes[0],data.Module,data.Teacher),"POST",h),
            "publish"=>await Send($"calendar/months/2026/11/classes/{data.Classes[0]}/publish",token,new CalendarCommand(Guid.NewGuid(),1,"Unauthorized"),"POST",h),
            "hours"=>await Send($"calendar/hours?classId={data.Classes[0]}",token,target:h),
            _=>await Send($"calendar/lessons/{lesson.Id}/history",token,target:h)
        };
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }
    [Fact] public async Task Calendar_StaffPublishesAndCombinedStaffTeacherDoesNotGainIamPrivileges()
    {
        await using var h=await CalendarHost();var admin=await Token();var data=await CalendarSetup(h,admin);
        var staff=await Token("Staff");await CalendarCreate(h,staff,data.Classes[0],data.Module,data.Teacher);await CalendarPublish(h,staff,data.Classes[0]);
        Assert.Single(await Ok<CalendarLesson[]>(await Send("calendar?from=2026-11-01&to=2026-11-30",staff,target:h)));
        await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher')",("@m",people["Staff"].Member));
        var mixed=await Token("Staff");Assert.Single(await Ok<CalendarLesson[]>(await Send("calendar?from=2026-11-01&to=2026-11-30",mixed,target:h)));
        Assert.Empty(await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",mixed,target:h)));
    }
    [Fact] public async Task Calendar_PublishedAmendmentCancellationRecoveryAndDeliveredCorrectionPreserveHistory()
    {
        await using var h=await CalendarHost();var admin=await Token();var data=await CalendarSetup(h,admin);
        var l=await CalendarCreate(h,admin,data.Classes[0],data.Module,data.Teacher);await CalendarPublish(h,admin,l.ClassId);
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h));
        await Ok<CalendarReceipt>(await Send($"calendar/lessons/{l.Id}/reschedule",admin,new CalendarRescheduleCommand(Guid.NewGuid(),l.Revision,new(2026,11,12),new(10,20),new(12,5),"Move"),"POST",h));
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h));Assert.Equal(105,l.DurationMinutes);
        await Ok<CalendarReceipt>(await Send($"calendar/lessons/{l.Id}/cancel",admin,new CalendarCommand(Guid.NewGuid(),l.Revision,"Teacher absent"),"POST",h));
        var recovery=CalendarLessonRequest(l.ClassId,l.ModuleId,data.Teacher,day:14);recovery=recovery with{Lesson=recovery.Lesson with{RecoveryOfLessonId=l.Id}};
        var replacement=await Ok<CalendarReceipt>(await Send("calendar/lessons",admin,recovery,"POST",h));await CalendarPublish(h,admin,l.ClassId);
        var r=await Ok<CalendarLesson>(await Send("calendar/lessons/"+replacement.ResourceId,admin,target:h));Assert.Equal(l.Id,r.RecoveryOfLessonId);
        var done=await Ok<CalendarReceipt>(await Send($"calendar/lessons/{r.Id}/complete",await Token("Teacher"),new CalendarDeliveredCommand(Guid.NewGuid(),r.Revision,140,"Delivered"),"POST",h));
        await Ok<CalendarReceipt>(await Send($"calendar/lessons/{r.Id}/correct-delivered-time",await Token("Staff"),new CalendarDeliveredCommand(Guid.NewGuid(),done.Revision,125,"Operational correction"),"POST",h));
        Assert.Equal(HttpStatusCode.Conflict,(await Send($"calendar/lessons/{r.Id}/reschedule",admin,new CalendarRescheduleCommand(Guid.NewGuid(),done.Revision+1,new(2026,11,16),new(9,0),new(11,0),"Invalid move"),"POST",h)).StatusCode);
        var hours=await Ok<CalendarHoursReport>(await Send($"calendar/hours?classId={l.ClassId}",admin,target:h));Assert.Equal(150,hours.ScheduledMinutes);Assert.Equal(125,hours.DeliveredMinutes);
        Assert.Equal(2,await Count("institution_lesson"));Assert.Equal(3,(await Ok<CalendarHistory[]>(await Send($"calendar/lessons/{l.Id}/history",admin,target:h))).Count(x=>x.Operation is "LessonPublished" or "LessonCancelled" or "LessonRescheduled"));
    }
    [Fact] public async Task Calendar_SubstitutionChangesRealTeacherViewWithoutChangingClassSubject()
    {
        await using var h=await CalendarHost();var admin=await Token();var data=await CalendarSetup(h,admin);var l=await CalendarCreate(h,admin,data.Classes[0],data.Module,data.Teacher);await CalendarPublish(h,admin,l.ClassId);
        await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Teacher');INSERT INTO institution_teacher_profile(institution_id,membership_id,teacher_id) VALUES(@t,@m,@teacher)",("@m",people["Staff"].Member),("@t",tenant),("@teacher",data.Substitute));
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h));
        await Ok<CalendarReceipt>(await Send($"calendar/lessons/{l.Id}/substitute",admin,new CalendarSubstituteCommand(Guid.NewGuid(),l.Revision,data.Substitute,"Cover"),"POST",h));
        Assert.Empty(await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",await Token("Teacher"),target:h)));
        Assert.Single(await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",await Token("Staff"),target:h)));
        await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();await using var cmd=new MySqlCommand("SELECT teacher_id FROM class_subject WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",l.ClassSubjectId);Assert.Equal(data.Teacher.ToString(),Convert.ToString(await cmd.ExecuteScalarAsync()));
        Assert.Equal(HttpStatusCode.Forbidden,(await Send($"calendar/lessons/{l.Id}/complete",await Token("Teacher"),new CalendarDeliveredCommand(Guid.NewGuid(),l.Revision+1,150,"Wrong teacher"),"POST",h)).StatusCode);
    }
    [Theory][InlineData("class")][InlineData("teacher")][InlineData("module")]
    public async Task Calendar_ForeignOrInventedReferencesCannotCreateLessons(string field)
    {
        await using var h=await CalendarHost();var token=await Token();var data=await CalendarSetup(h,token);var r=CalendarLessonRequest(data.Classes[0],data.Module,data.Teacher);
        r=r with{Lesson=field switch{"class"=>r.Lesson with{ClassId=Guid.NewGuid()},"teacher"=>r.Lesson with{TeacherId=Guid.NewGuid()},_=>r.Lesson with{ModuleId=Guid.NewGuid()}}};
        var result=await Send("calendar/lessons",token,r,"POST",h);Assert.True(result.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict);Assert.Equal(0,await Count("institution_lesson"));
    }
    [Fact] public async Task Calendar_ConcurrentOverlappingManualWritesHaveOneWinnerAndAdjacentLessonsAreAllowed()
    {
        await using var h=await CalendarHost();var token=await Token();var data=await CalendarSetup(h,token);
        var results=await Task.WhenAll(data.Classes.Take(2).Select(c=>Send("calendar/lessons",token,CalendarLessonRequest(c,data.Module,data.Teacher),"POST",h)));
        Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.Conflict));Assert.Equal(1,await Count("institution_lesson"));
        var adjacent=CalendarLessonRequest(data.Classes[2],data.Module,data.Teacher);adjacent=adjacent with{Lesson=adjacent.Lesson with{StartsAt=new(11,45),EndsAt=new(13,0)}};
        await Ok<CalendarReceipt>(await Send("calendar/lessons",token,adjacent,"POST",h));
    }
    [Fact] public async Task Calendar_ConcurrentPublicationAndConcurrentCompletionAreRevisionProtected()
    {
        await using var h=await CalendarHost();var token=await Token();var data=await CalendarSetup(h,token);var l=await CalendarCreate(h,token,data.Classes[0],data.Module,data.Teacher);
        var p=await Ok<CalendarPeriod>(await Send($"calendar/months/2026/11/classes/{l.ClassId}",token,target:h));
        var results=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Send($"calendar/months/2026/11/classes/{l.ClassId}/publish",token,new CalendarCommand(Guid.NewGuid(),p.Revision,"Concurrent publication"),"POST",h)));
        Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.Conflict));
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,token,target:h));var teacher=await Token("Teacher");var r=new CalendarDeliveredCommand(Guid.NewGuid(),l.Revision,135,"Complete once");
        var completed=await Task.WhenAll(Send($"calendar/lessons/{l.Id}/complete",teacher,r,"POST",h),Send($"calendar/lessons/{l.Id}/complete",teacher,r,"POST",h));
        Assert.All(completed,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode));Assert.Equal(135,(await Ok<CalendarHoursReport>(await Send($"calendar/hours?classId={l.ClassId}",token,target:h))).DeliveredMinutes);
    }
    [Fact] public async Task Calendar_InvalidImportIsAllOrNothingAndXlsxImportPersists()
    {
        await using var h=await CalendarHost();var token=await Token();var data=await CalendarSetup(h,token);
        var invalid=await CalendarUpload(h,token,new[]{data.Classes[0]},$"Data;OraInizio;OraFine;Classe;Modulo;Docente\n2026-11-05;09:00;11:00;{data.Classes[0]};{data.Module};{data.Teacher}\n2026-11-05;11:00;12:00;{data.Classes[0]};{Guid.NewGuid()};{data.Teacher}");
        Assert.False(invalid.CanConfirm);Assert.Equal(HttpStatusCode.Conflict,(await Send($"calendar/imports/{invalid.Id}/confirm",token,new CalendarConfirmImport(Guid.NewGuid(),0,invalid.Digest,true,"Invalid import"),"POST",h)).StatusCode);Assert.Equal(0,await Count("institution_lesson"));
        var bytes=InstitutionImportXlsxReaderTests.Workbook(new[]{new[]{"Data","OraInizio","OraFine","Classe","Modulo","Docente"},new[]{"2026-11-05","09:15","11:45",data.Classes[0].ToString(),data.Module.ToString(),data.Teacher.ToString()}});
        var valid=await CalendarUpload(h,token,new[]{data.Classes[0]},"",extension:"xlsx",bytes:bytes);Assert.True(valid.CanConfirm);
        await Ok<CalendarReceipt>(await Send($"calendar/imports/{valid.Id}/confirm",token,new CalendarConfirmImport(Guid.NewGuid(),0,valid.Digest,true,"Xlsx confirm"),"POST",h));Assert.Equal(1,await Count("institution_lesson"));
    }
    [Fact] public async Task Calendar_CompetingImportConfirmationsRevalidateTeacherBookings()
    {
        await using var h=await CalendarHost();var token=await Token();var data=await CalendarSetup(h,token);
        async Task<CalendarImportPreview> Preview(Guid c)=>await CalendarUpload(h,token,new[]{c},$"Data;OraInizio;OraFine;Classe;Modulo;Docente\n2026-11-05;09:00;11:00;{c};{data.Module};{data.Teacher}");
        var previews=await Task.WhenAll(data.Classes.Take(2).Select(Preview));Assert.All(previews,p=>Assert.True(p.CanConfirm));
        var result=await Task.WhenAll(previews.Select(p=>Send($"calendar/imports/{p.Id}/confirm",token,new CalendarConfirmImport(Guid.NewGuid(),0,p.Digest,true,"Concurrent import"),"POST",h)));
        Assert.Single(result.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(result.Where(r=>r.StatusCode==HttpStatusCode.Conflict));Assert.Equal(1,await Count("institution_lesson"));
    }
    [Fact] public async Task Calendar_ReplaceExactDraftScopePreservesOtherClassesAndPublishedRecords()
    {
        await using var h=await CalendarHost();var token=await Token();var data=await CalendarSetup(h,token);
        var old=await CalendarCreate(h,token,data.Classes[0],data.Module,data.Teacher);var otherLesson=await CalendarCreate(h,token,data.Classes[1],data.Module,data.Teacher,day:6);
        var p=await CalendarUpload(h,token,new[]{data.Classes[0]},$"Data;OraInizio;OraFine;Classe;Modulo;Docente\n2026-11-05;09:15;11:45;{data.Classes[0]};{data.Module};{data.Teacher}",CalendarImportMode.ReplaceDraftScope);
        Assert.True(p.CanConfirm);Assert.Equal(1,p.Replaced);
        await Ok<CalendarReceipt>(await Send($"calendar/imports/{p.Id}/confirm",token,new CalendarConfirmImport(Guid.NewGuid(),0,p.Digest,true,"Replace exact scope"),"POST",h));
        Assert.Equal("Cancelled",(await Ok<CalendarLesson>(await Send("calendar/lessons/"+old.Id,token,target:h))).Status);
        Assert.Equal("Scheduled",(await Ok<CalendarLesson>(await Send("calendar/lessons/"+otherLesson.Id,token,target:h))).Status);Assert.Equal(3,await Count("institution_lesson"));
    }
    private sealed class CalendarFault(string point):ICalendarFaultObserver
    {public Task CheckpointAsync(string name,CancellationToken ct){if(name==point)throw new CalendarConflictException("calendar.injected-fault");return Task.CompletedTask;}}
    [Theory][InlineData("after-audit")][InlineData("after-lesson")][InlineData("before-commit")]
    public async Task Calendar_FaultInjectionRollsBackRowsPeriodsAndAudit(string point)
    {
        await using var normal=await CalendarHost();var token=await Token();var data=await CalendarSetup(normal,token);var count=await Count("institution_calendar_event");
        await using var failing=await CalendarHost(s=>s.AddSingleton<ICalendarFaultObserver>(new CalendarFault(point)));
        Assert.Equal(HttpStatusCode.Conflict,(await Send("calendar/lessons",token,CalendarLessonRequest(data.Classes[0],data.Module,data.Teacher),"POST",failing)).StatusCode);
        Assert.Equal(0,await Count("institution_lesson"));Assert.Equal(0,await Count("institution_calendar_period"));Assert.Equal(count,await Count("institution_calendar_event"));
    }
    [Fact] public async Task Calendar_RevocationInvalidatesOldTokenAndAuditCannotBeDeleted()
    {
        await using var h=await CalendarHost();var token=await Token();var data=await CalendarSetup(h,token);await CalendarCreate(h,token,data.Classes[0],data.Module,data.Teacher);
        var error=await Assert.ThrowsAsync<MySqlException>(()=>Sql("DELETE FROM institution_calendar_event WHERE institution_id=@t",("@t",tenant)));Assert.Equal(1644,error.Number);
        await Sql("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='InstitutionAdmin'",("@m",people["InstitutionAdmin"].Member));
        Assert.Equal(HttpStatusCode.Forbidden,(await Send("calendar/lessons",token,CalendarLessonRequest(data.Classes[1],data.Module,data.Teacher,day:6),"POST",h)).StatusCode);Assert.Equal(1,await Count("institution_lesson"));
    }
    [Fact] public async Task Calendar_DirectUnauditedSqlMutationAndReservedJsonPropertiesAreRejected()
    {
        await using var h=await CalendarHost();var token=await Token();var data=await CalendarSetup(h,token);var l=await CalendarCreate(h,token,data.Classes[0],data.Module,data.Teacher);
        var error=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE institution_lesson SET publication='Published',revision=revision+1 WHERE id=@id",("@id",l.Id)));Assert.Equal(1644,error.Number);
        var request=CalendarLessonRequest(data.Classes[1],data.Module,data.Teacher,day:6);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send("calendar/lessons",token,new{request.EventId,request.ExpectedRevision,request.Lesson,request.Reason,InstitutionId=other},"POST",h)).StatusCode);
        using var scope=h.Host.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Single(await db.Set<InstitutionLessonRecord>().Where(x=>x.InstitutionId==tenant).ToArrayAsync());
    }
}
