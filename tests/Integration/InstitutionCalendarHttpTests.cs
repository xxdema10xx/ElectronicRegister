using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using ElectronicRegisterAPI.Domain.Enums;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionFormativePlanTests
{
    private Task<Mt3cHosting.ProgramHost> CalendarHost(Action<IServiceCollection>? configure=null)=>Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,tenant,Array.Empty<Guid>(),enabled:false,school:true,course:true,
        configureServices:configure,extraArguments:new[]{"--Multitenancy:FormativePlans:Enabled=true","--Multitenancy:InstitutionCalendar:Enabled=true"});
    private async Task<(Guid[] Classes,Guid Teacher,Guid Substitute,Guid Module)> CalendarSetup(Mt3cHosting.ProgramHost target,string token)
    {
        var plan=await Published();await Ok<FormativeReceipt>(await Send($"course-editions/{edition}/formative-plan",token,new BindFormativeVersion(Guid.NewGuid(),plan.Id,"Calendar binding"),"POST"));
        var area=Guid.NewGuid();var biennium=Guid.NewGuid();var bsa=Guid.NewGuid();var bsp=Guid.NewGuid();var teacher=Guid.NewGuid();var substitute=Guid.NewGuid();var student=Guid.NewGuid();
        var classes=Enumerable.Range(0,3).Select(_=>Guid.NewGuid()).ToArray();
        await Sql("INSERT INTO study_area(id,institution_id,name,description) VALUES(@a,@t,'Calendar area','Synthetic');INSERT INTO biennium(id,institution_id,start_year,end_year) VALUES(@b,@t,'2025-01-01','2027-01-01');INSERT INTO biennium_study_area(id,institution_id,biennium_id,study_area_id) VALUES(@bsa,@t,@b,@a);INSERT INTO biennium_study_path(id,institution_id,biennium_study_area_id,study_path_id) VALUES(@bsp,@t,@bsa,@p);INSERT INTO teacher(id,institution_id,first_name,last_name) VALUES(@teacher,@t,'Scheduled','Teacher'),(@sub,@t,'Cover','Teacher')",
            ("@a",area),("@t",tenant),("@b",biennium),("@bsa",bsa),("@bsp",bsp),("@p",path),("@teacher",teacher),("@sub",substitute));
        for(var i=0;i<classes.Length;i++)await Sql("INSERT INTO class(id,institution_id,biennium_study_path_id,course_edition_id,name) VALUES(@id,@t,@bsp,@e,@name);INSERT INTO class_subject(id,institution_id,class_id,subject_id,teacher_id) VALUES(@cs,@t,@id,@subject,@teacher)",("@id",classes[i]),("@t",tenant),("@bsp",bsp),("@e",edition),("@name","CAL-"+i),("@cs",Guid.NewGuid()),("@subject",subject),("@teacher",teacher));
        await Sql("INSERT INTO institution_teacher_profile(institution_id,membership_id,teacher_id) VALUES(@t,@m,@teacher);INSERT INTO student(id,institution_id,first_name,last_name,class_id,class_institution_id) VALUES(@s,@t,'Calendar','Student',@class,@t);INSERT INTO institution_student_profile(institution_id,membership_id,student_id) VALUES(@t,@sm,@s)",("@t",tenant),("@m",people["Teacher"].Member),("@teacher",teacher),("@s",student),("@class",classes[0]),("@sm",people["Student"].Member));
        await Ok<CalendarReceipt>(await Send("calendar/settings",token,new CalendarSettingsCommand(Guid.NewGuid(),-1,"Europe/Rome",3,"Calendar settings"),"PUT",target));
        return(classes,teacher,substitute,plan.Units[0].Modules[0].Id);
    }
    private async Task<CalendarImportPreview> CalendarUpload(Mt3cHosting.ProgramHost target,string token,Guid[] classes,string csv,CalendarImportMode mode=CalendarImportMode.AppendDraft,string extension="csv",byte[]? bytes=null)
    {
        using var content=new MultipartFormDataContent();content.Add(new ByteArrayContent(bytes??Encoding.UTF8.GetBytes(csv)),"file","calendar."+extension);
        content.Add(new StringContent(JsonSerializer.Serialize(new CalendarImportSettings(Guid.NewGuid(),2026,11,classes,mode,null,"Calendar preview"))),"settings");
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/institution/calendar/imports/preview"){Content=content};request.Headers.Authorization=new("Bearer",token);
        return await Ok<CalendarImportPreview>(await target.Client.SendAsync(request));
    }
    private CalendarSaveLesson CalendarLessonRequest(Guid c,Guid m,Guid teacher,int hour=9,int day=5)=>new(Guid.NewGuid(),-1,new(c,m,teacher,new(2026,11,day),new(hour,15),new(hour+2,45)),"Calendar manual lesson");

    [Fact] public async Task Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit()
    {
        await using var target=await CalendarHost();var admin=await Token();var data=await CalendarSetup(target,admin);
        var csv="Data;OraInizio;OraFine;Classe;Modulo;Docente\n"+string.Join('\n',data.Classes.Select((c,i)=>$"2026-11-{5+i:00};09:15;11:45;{c};{data.Module};{data.Teacher}"));
        var preview=await CalendarUpload(target,admin,data.Classes,csv);Assert.True(preview.CanConfirm);Assert.Equal(0,await Count("institution_lesson"));
        Assert.Equal(3,preview.ValidRows);Assert.Equal("Data",preview.Columns![0]);Assert.NotEmpty(preview.ResolvedMapping!);
        Assert.Equal(data.Teacher.ToString(),preview.Rows[0].TeacherReference);
        var confirm=new CalendarConfirmImport(Guid.NewGuid(),preview.Revision,preview.Digest,true,"Confirm calendar");
        var receipt=await Ok<CalendarReceipt>(await Send($"calendar/imports/{preview.Id}/confirm",admin,confirm,"POST",target));
        var replay=await Ok<CalendarReceipt>(await Send($"calendar/imports/{preview.Id}/confirm",admin,confirm,"POST",target));
        Assert.Equal(JsonSerializer.Serialize(receipt),JsonSerializer.Serialize(replay));Assert.Equal(3,await Count("institution_lesson"));
        var teacherToken=await Token("Teacher");var studentToken=await Token("Student");
        Assert.Empty(await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",teacherToken,target:target)));
        foreach(var c in data.Classes)
        {
            var p=await Ok<CalendarPeriod>(await Send($"calendar/months/2026/11/classes/{c}",admin,target:target));
            await Ok<CalendarReceipt>(await Send($"calendar/months/2026/11/classes/{c}/publish",admin,new CalendarCommand(Guid.NewGuid(),p.Revision,"Explicit publish"),"POST",target));
        }
        var lessons=await Ok<CalendarLesson[]>(await Send("calendar/teachers/me?from=2026-11-01&to=2026-11-30",teacherToken,target:target));Assert.Equal(3,lessons.Length);Assert.Equal(3,lessons.Select(x=>x.ClassId).Distinct().Count());
        Assert.Single(await Ok<CalendarLesson[]>(await Send("calendar/students/me?from=2026-11-01&to=2026-11-30",studentToken,target:target)));
        var first=lessons.Single(l=>l.ClassId==data.Classes[0]);var complete=new CalendarDeliveredCommand(Guid.NewGuid(),first.Revision,125,"Delivered confirmation");
        await Ok<CalendarReceipt>(await Send($"calendar/lessons/{first.Id}/complete",teacherToken,complete,"POST",target));
        var hours=await Ok<CalendarHoursReport>(await Send($"calendar/hours?classId={first.ClassId}",admin,target:target));Assert.Equal(72000,hours.PlannedMinutes);Assert.Equal(150,hours.ScheduledMinutes);Assert.Equal(125,hours.DeliveredMinutes);
        var history=await Ok<CalendarHistory[]>(await Send($"calendar/lessons/{first.Id}/history",admin,target:target));Assert.Contains(history,e=>e.Operation=="LessonCompleted");
        Assert.Equal(HttpStatusCode.Forbidden,(await Send($"calendar/lessons/{first.Id}/history",teacherToken,target:target)).StatusCode);
    }
}
