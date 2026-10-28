using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionFormativePlanTests
{
    [Fact] public async Task Calendar_NativeDeadlockLeavesOneDurableLessonAndSameIntentCanRetry()
    {
        await using var setup=await CalendarHost();var admin=await Token();var d=await CalendarSetup(setup,admin);
        var barrier=new CalendarPause("after-iam-lock");await using var h=await CalendarHost(s=>s.AddSingleton<ICalendarFaultObserver>(barrier));
        await using var connection=new MySqlConnection(fixture.ConnectionString);await connection.OpenAsync();await using var tx=await connection.BeginTransactionAsync();
        await using(var teacherLock=new MySqlCommand("SELECT id FROM teacher WHERE institution_id=@t AND id=@id FOR UPDATE",connection,tx))
        {teacherLock.Parameters.AddWithValue("@t",tenant);teacherLock.Parameters.AddWithValue("@id",d.Teacher);await teacherLock.ExecuteScalarAsync();}
        var request=CalendarLessonRequest(d.Classes[0],d.Module,d.Teacher);var write=Send("calendar/lessons",admin,request,"POST",h);
        await barrier.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));barrier.Release.TrySetResult(true);await Task.Delay(100);
        var nativeVictim=false;
        await using(var institutionLock=new MySqlCommand("SELECT id FROM institution WHERE id=@id FOR UPDATE",connection,tx){CommandTimeout=10})
        {
            institutionLock.Parameters.AddWithValue("@id",tenant);
            try{await institutionLock.ExecuteScalarAsync();}
            catch(MySqlException e)when(e.Number==1213){nativeVictim=true;}
        }
        await tx.RollbackAsync();var response=await write.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(nativeVictim||response.StatusCode==HttpStatusCode.Conflict);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);
        // Both choices of deadlock victim converge to the same actor-bound receipt.
        var receipt=await Ok<CalendarReceipt>(await Send("calendar/lessons",admin,request,"POST",setup));
        Assert.Equal(request.EventId,receipt.EventId);Assert.Equal(1,await Count("institution_lesson"));
    }
    [Fact] public async Task Calendar_TwoAdministratorsAndPublicationVersusSubstitutionSerialize()
    {
        await using var h=await CalendarHost();var admin=await Token();var d=await CalendarSetup(h,admin);var l=await CalendarCreate(h,admin,d.Classes[0],d.Module,d.Teacher);
        await Sql("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'InstitutionAdmin')",("@m",people["Staff"].Member));var second=await Token("Staff");
        var p=await Ok<CalendarPeriod>(await Send($"calendar/months/2026/11/classes/{l.ClassId}",admin,target:h));
        var results=await Task.WhenAll(Send($"calendar/months/2026/11/classes/{l.ClassId}/publish",admin,new CalendarCommand(Guid.NewGuid(),p.Revision,"First administrator"),"POST",h),
            Send($"calendar/months/2026/11/classes/{l.ClassId}/publish",second,new CalendarCommand(Guid.NewGuid(),p.Revision,"Second administrator"),"POST",h));
        Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(results.Where(r=>r.StatusCode==HttpStatusCode.Conflict));
        l=await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h));
        var updated=await Ok<CalendarReceipt>(await Send($"calendar/lessons/{l.Id}/substitute",second,new CalendarSubstituteCommand(Guid.NewGuid(),l.Revision,d.Substitute,"Published cover"),"POST",h));
        var period=await Ok<CalendarPeriod>(await Send($"calendar/months/2026/11/classes/{l.ClassId}",admin,target:h));
        Assert.Equal(people["Staff"].Member,period.PublishedByMembershipId);Assert.Equal(updated.Revision,(await Ok<CalendarLesson>(await Send("calendar/lessons/"+l.Id,admin,target:h))).Revision);
        var draft=await CalendarCreate(h,admin,d.Classes[1],d.Module,d.Teacher,day:6);
        var draftPeriod=await Ok<CalendarPeriod>(await Send($"calendar/months/2026/11/classes/{draft.ClassId}",admin,target:h));
        var race=await Task.WhenAll(Send($"calendar/months/2026/11/classes/{draft.ClassId}/publish",admin,new CalendarCommand(Guid.NewGuid(),draftPeriod.Revision,"Publication race"),"POST",h),
            Send($"calendar/lessons/{draft.Id}/substitute",second,new CalendarSubstituteCommand(Guid.NewGuid(),draft.Revision,d.Substitute,"Concurrent substitution"),"POST",h));
        Assert.Single(race.Where(r=>r.StatusCode==HttpStatusCode.OK));Assert.Single(race.Where(r=>r.StatusCode==HttpStatusCode.Conflict));
        draft=await Ok<CalendarLesson>(await Send("calendar/lessons/"+draft.Id,admin,target:h));
        if(draft.Publication=="Draft")await CalendarPublish(h,admin,draft.ClassId);
        else await Ok<CalendarReceipt>(await Send($"calendar/lessons/{draft.Id}/substitute",second,new CalendarSubstituteCommand(Guid.NewGuid(),draft.Revision,d.Substitute,"Explicit retry after publication"),"POST",h));
        Assert.Equal(d.Substitute,(await Ok<CalendarLesson>(await Send("calendar/lessons/"+draft.Id,admin,target:h))).TeacherId);
    }
}
