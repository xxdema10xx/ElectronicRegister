using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionFormativePlanTests
{
    [Fact] public async Task Calendar_PublishingOneClassDoesNotExposeOtherDraftsAndBudgetsStaySeparate()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);
        for(var i=0;i<3;i++)await CalendarCreate(h,admin,d.Classes[i],d.Module,d.Teacher,5+i);
        await CalendarPublish(h,admin,d.Classes[0]);var teacher=await Token("Teacher");var student=await Token("Student");
        Assert.Single(await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",teacher,target:h)));
        Assert.Empty(await Ok<CalendarLesson[]>(await Send($"calendar/students/me?from=2026-11-01&to=2026-11-30&classId={d.Classes[1]}",student,target:h)));
        var a=await Ok<CalendarHoursReport>(await Send($"calendar/hours?classId={d.Classes[0]}",admin,target:h));var b=await Ok<CalendarHoursReport>(await Send($"calendar/hours?classId={d.Classes[1]}",admin,target:h));
        Assert.Equal(72000,a.PlannedMinutes);Assert.Equal(72000,b.PlannedMinutes);Assert.Equal(150,a.ScheduledMinutes);Assert.Equal(0,b.ScheduledMinutes);
        Assert.Equal(0,a.DeliveredMinutes);Assert.Equal(a.ScheduledMinutes,Assert.Single(a.Months).ScheduledMinutes);Assert.Equal(a.PlannedMinutes,Assert.Single(a.Units).PlannedMinutes);
    }
    [Fact] public async Task Calendar_PublishedChangesAppearInBothPersonalViewsAndReasonsRemainPrivate()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);var l=await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);await CalendarPublish(h,admin,l.ClassId);
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h));
        await Ok<CalendarReceipt>(await Send($"calendar/lessons/{l.Id}/reschedule",admin,new CalendarRescheduleCommand(Guid.NewGuid(),l.Revision,new(2026,12,3),new(13,30),new(17,10),"Private operational reason"),"POST",h));
        foreach(var route in new[]{("teachers/me","Teacher"),("students/me","Student")})
        {
            var response=await Send("calendar/"+route.Item1+"?from=2026-12-01&to=2026-12-31",await Token(route.Item2),target:h);
            var text=await response.Content.ReadAsStringAsync();Assert.DoesNotContain("Private operational reason",text);Assert.DoesNotContain("actorMembershipId",text);
            var updated=await Ok<CalendarLesson[]>(response);Assert.Equal(220,Assert.Single(updated).DurationMinutes);Assert.Equal(l.Id,updated[0].Id);
        }
    }
    [Theory][InlineData("digest")][InlineData("revision")][InlineData("assignment")][InlineData("private-owner")]
    public async Task Calendar_ImportCommitRejectsUntrustedOrStalePreview(string change)
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);
        var p=await CalendarUpload(h,admin,new[]{d.Classes[0]},$"Data;OraInizio;OraFine;Classe;Modulo;Docente\n2026-11-05;09:15;11:45;{d.Classes[0]};{d.Module};{d.Teacher}");
        if(change=="assignment")await Sql("UPDATE class_subject SET teacher_id=@teacher WHERE institution_id=@t AND class_id=@c",("@teacher",d.Substitute),("@t",tenant),("@c",d.Classes[0]));
        var r=new CalendarConfirmImport(Guid.NewGuid(),change=="revision"?1:0,change=="digest"?new string('A',64):p.Digest,true,"Commit preview");
        var result=await Send($"calendar/imports/{p.Id}/confirm",change=="private-owner"?await Token("Staff"):admin,r,"POST",h);
        Assert.Equal(change=="private-owner"?HttpStatusCode.NotFound:HttpStatusCode.Conflict,result.StatusCode);Assert.Equal(0,await Count("institution_lesson"));
    }
    [Fact] public async Task Calendar_SameEventWithDifferentPayloadConflictsAndCompletedReplayDoesNotDoubleHours()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);var r=CalendarLessonRequest(d.Classes[0],d.Module,d.Teacher);
        var first=await Ok<CalendarReceipt>(await Send("calendar/lessons",admin,r,"POST",h));
        Assert.Equal(first,await Ok<CalendarReceipt>(await Send("calendar/lessons",admin,r,"POST",h)));
        Assert.Equal(HttpStatusCode.Conflict,(await Send("calendar/lessons",admin,r with{Reason="Different intent"},"POST",h)).StatusCode);Assert.Equal(1,await Count("institution_lesson"));
    }
    private sealed class CalendarPause(string point):ICalendarFaultObserver
    {
        public TaskCompletionSource<bool> Acquired {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task CheckpointAsync(string name,CancellationToken ct){if(name==point){Acquired.TrySetResult(true);await Release.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);}}
    }
    [Fact] public async Task Calendar_IamRevocationWaitsForAuthorizedCommitThenRejectsOldToken()
    {
        await using var setup=await CalendarHost();var admin=await Token();var d=await CalendarSetup(setup,admin);var staff=await Token("Staff");
        var barrier=new CalendarPause("after-iam-lock");await using var h=await CalendarHost(s=>s.AddSingleton<ICalendarFaultObserver>(barrier));
        var write = SqlLockEvidence.Track(() => Send("calendar/lessons",staff,CalendarLessonRequest(d.Classes[0],d.Module,d.Teacher),"POST",h));await barrier.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var revoke = SqlLockEvidence.Track(() => Sql("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='Staff'",("@m",people["Staff"].Member)));
        try{await SqlLockEvidence.WaitAsync(fixture.ConnectionString, revoke, write);}finally{barrier.Release.TrySetResult(true);}
        await Ok<CalendarReceipt>(await write);await revoke.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.Forbidden,(await Send("calendar/lessons",staff,CalendarLessonRequest(d.Classes[1],d.Module,d.Teacher,day:6),"POST",setup)).StatusCode);
    }
    [Fact] public async Task Calendar_RuntimeRolloutClosureBeforeCommitRollsBackAllWrites()
    {
        await using var setup=await CalendarHost();var admin=await Token();var d=await CalendarSetup(setup,admin);var count=await Count("institution_calendar_event");
        var barrier=new CalendarPause("before-commit");await using var h=await CalendarHost(s=>s.AddSingleton<ICalendarFaultObserver>(barrier));
        var write = SqlLockEvidence.Track(() => Send("calendar/lessons",admin,CalendarLessonRequest(d.Classes[0],d.Module,d.Teacher),"POST",h));await barrier.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        h.Host.Services.GetRequiredService<IConfiguration>()["Multitenancy:InstitutionCalendar:Enabled"]="false";barrier.Release.TrySetResult(true);
        Assert.Equal(HttpStatusCode.Forbidden,(await write).StatusCode);Assert.Equal(0,await Count("institution_lesson"));Assert.Equal(count,await Count("institution_calendar_event"));
    }
    [Fact] public async Task Calendar_PublicationFaultRollsBackVisibilityAndOccupancy()
    {
        await using var setup=await CalendarHost();var admin=await Token();var d=await CalendarSetup(setup,admin);var l=await CalendarCreate(setup,admin,d.Classes[0],d.Module,d.Teacher);
        var p=await Ok<CalendarPeriod>(await Send($"calendar/months/2026/11/classes/{l.ClassId}",admin,target:setup));var count=await Count("institution_calendar_event");
        await using var h=await CalendarHost(s=>s.AddSingleton<ICalendarFaultObserver>(new CalendarFault("after-publication")));
        Assert.Equal(HttpStatusCode.Conflict,(await Send($"calendar/months/2026/11/classes/{l.ClassId}/publish",admin,new CalendarCommand(Guid.NewGuid(),p.Revision,"Fail publication"),"POST",h)).StatusCode);
        Assert.Equal("Draft",(await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:setup))).Publication);
        Assert.Equal(count,await Count("institution_calendar_event"));Assert.Equal(0,await Count("institution_calendar_occupancy"));
    }
    [Fact] public async Task Calendar_ClosedStartupHasNoRoutesAndSelectionTokenCannotWrite()
    {
        Assert.Equal(HttpStatusCode.NotFound,(await Send("calendar/settings",await Token())).StatusCode);
        await using var h=await CalendarHost();Assert.Equal(HttpStatusCode.Unauthorized,(await Send("calendar/settings",await Token(selection:true),target:h)).StatusCode);
    }
}
