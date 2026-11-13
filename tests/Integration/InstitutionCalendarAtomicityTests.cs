using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionFormativePlanTests
{
    private sealed class CalendarSecondRowFault:ICalendarFaultObserver
    {
        private int count;
        public Task CheckpointAsync(string name,CancellationToken ct)
        {if(name=="after-lesson"&&Interlocked.Increment(ref count)==2)throw new CalendarConflictException("calendar.injected-second-row-fault");return Task.CompletedTask;}
    }
    [Fact] public async Task Calendar_ImportFaultOnSecondLessonRollsBackEntireBatchAndRetainsPreview()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);
        var p=await CalendarUpload(h,admin,new[]{d.Classes[0]},$"Data;OraInizio;OraFine;Classe;Modulo;Docente\n2026-11-05;09:15;11:45;{d.Classes[0]};{d.Module};{d.Teacher}\n2026-11-06;09:15;11:45;{d.Classes[0]};{d.Module};{d.Teacher}");
        var count=await Count("institution_calendar_event");await using var bad=await CalendarHost(s=>s.AddSingleton<ICalendarFaultObserver>(new CalendarSecondRowFault()));
        Assert.Equal(HttpStatusCode.Conflict,(await Send($"calendar/imports/{p.Id}/confirm",admin,new CalendarConfirmImport(Guid.NewGuid(),0,p.Digest,true,"Atomic import fault"),"POST",bad)).StatusCode);
        Assert.Equal(0,await Count("institution_lesson"));Assert.Equal(0,await Count("institution_calendar_period"));Assert.Equal(count,await Count("institution_calendar_event"));
        Assert.Equal("Preview",(await Ok<CalendarImportPreview>(await Send("calendar/imports/"+p.Id,admin,target:h))).Status);
    }
    [Fact] public async Task Calendar_CancellationAndCompletionRaceCannotProduceCancelledDeliveredLesson()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);var l=await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);await CalendarPublish(h,admin,l.ClassId);
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h));var teacher=await Token("Teacher");
        var replies=await Task.WhenAll(Send($"calendar/lessons/{l.Id}/cancel",admin,new CalendarCommand(Guid.NewGuid(),l.Revision,"Cancellation race"),"POST",h),
            Send($"calendar/lessons/{l.Id}/complete",teacher,new CalendarDeliveredCommand(Guid.NewGuid(),l.Revision,135,"Completion race"),"POST",h));
        Assert.Single(replies.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(replies.Where(r=>r.StatusCode==HttpStatusCode.Conflict));
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h));Assert.True(l.Status=="Cancelled"&&l.DeliveredMinutes is null||l.Status=="Completed"&&l.DeliveredMinutes==135);
    }
    [Fact] public async Task Calendar_LegacyClassAndModuleFromAnotherPublishedVersionAreRejected()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);var legacy=Guid.NewGuid();
        await Sql("INSERT INTO class(id,institution_id,biennium_study_path_id,name) SELECT @id,institution_id,biennium_study_path_id,'LEGACY' FROM class WHERE id=@source",("@id",legacy),("@source",d.Classes[0]));
        Assert.Equal(HttpStatusCode.Conflict,(await Send("calendar/lessons",admin,CalendarLessonRequest(legacy,d.Module,d.Teacher),"POST",h)).StatusCode);
        var source=await Ok<FormativePlanSnapshot>(await Send($"course-editions/{edition}/formative-plan",admin));
        var clone=await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{source.Id}/clone",admin,new FormativeCommand(Guid.NewGuid(),source.Revision,"Independent version"),"POST"));
        await Ok<FormativeReceipt>(await Send($"formative-plan-versions/{clone.VersionId}/publish",admin,new FormativeCommand(Guid.NewGuid(),0,"Independent publication"),"POST"));
        var second=await Version(clone.VersionId!.Value,admin);Assert.NotEqual(d.Module,second.Units[0].Modules[0].Id);
        Assert.Equal(HttpStatusCode.Conflict,(await Send("calendar/lessons",admin,CalendarLessonRequest(d.Classes[0],second.Units[0].Modules[0].Id,d.Teacher),"POST",h)).StatusCode);Assert.Equal(0,await Count("institution_lesson"));
    }
    [Fact] public async Task Calendar_ConcurrentImportAndManualBookingCannotDoubleBookTeacher()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);
        var p=await CalendarUpload(h,admin,new[]{d.Classes[0]},$"Data;OraInizio;OraFine;Classe;Modulo;Docente\n2026-11-05;09:15;11:45;{d.Classes[0]};{d.Module};{d.Teacher}");
        var results=await Task.WhenAll(Send($"calendar/imports/{p.Id}/confirm",admin,new CalendarConfirmImport(Guid.NewGuid(),0,p.Digest,true,"Concurrent import"),"POST",h),
            Send("calendar/lessons",admin,CalendarLessonRequest(d.Classes[1],d.Module,d.Teacher),"POST",h));
        Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.Conflict));Assert.Equal(1,await Count("institution_lesson"));
    }
}
