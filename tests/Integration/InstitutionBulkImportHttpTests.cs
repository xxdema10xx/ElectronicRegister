using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using StudentEnrollment=ElectronicRegisterAPI.Infrastructure.Persistence.Entities.StudentEnrollment;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    private const string Imports="/api/institution/imports";
    private Task<Mt3cHosting.ProgramHost> ImportHost(bool enabled=true)=>Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,exam:true,course:true,
        extraArguments:new[]{"--Multitenancy:InstitutionEmail:Enabled=true","--Multitenancy:InstitutionImport:Enabled="+enabled});
    private async Task<HttpResponseMessage> UploadImport(Mt3cHosting.ProgramHost host,string token,byte[] bytes,ImportPreviewSettings settings,string filename="people.csv")
    {
        using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(bytes),"file",filename);form.Add(new StringContent(JsonSerializer.Serialize(settings)),"settings");
        using var request=new HttpRequestMessage(HttpMethod.Post,Imports+"/preview"){Content=form};request.Headers.Authorization=new("Bearer",token);return await host.Client.SendAsync(request);
    }
    private async Task<ImportBatch> PreviewImport(Mt3cHosting.ProgramHost host,string token,string csv,ImportPreviewSettings? settings=null)
    {var response=await UploadImport(host,token,Encoding.UTF8.GetBytes(csv),settings??new(ImportCategory.Student));await Expect(response,HttpStatusCode.Created);return(await response.Content.ReadFromJsonAsync<ImportBatch>())!;}
    private async Task<HttpResponseMessage> ConfirmImport(Mt3cHosting.ProgramHost host,string token,ImportBatch batch,Guid? eventId=null,bool acknowledge=true,bool repeat=false,string? reason=null)=>
        await Request(Imports+"/"+batch.Id+"/confirm",token,new ImportConfirmation(eventId??Guid.NewGuid(),batch.Revision,batch.PreviewDigest,acknowledge,repeat,reason),"POST",host);
    [Theory]
    [InlineData(ImportCategory.Student,false)][InlineData(ImportCategory.Teacher,false)][InlineData(ImportCategory.Student,true)][InlineData(ImportCategory.Teacher,true)]
    public async Task BulkImport_PreviewIsPrivateAndPureThenConfirmationCreatesOnlyAnagraphics(ImportCategory category,bool staff)
    {
        await using var host=await ImportHost();var token=staff?await StaffToken():await Access();long users=0,members=0,enrollments=0,subjects=0;
        await Db(async db=>{users=await db.Users.LongCountAsync();members=await db.InstitutionMemberships.LongCountAsync();enrollments=await db.Set<StudentEnrollment>().LongCountAsync();subjects=await db.ClassSubjects.LongCountAsync();});
        var before=await Digest(false);var b=await PreviewImport(host,token,"Nome;Cognome;RiferimentoEsterno\nMario;Rossi;P1\nAnna Maria;Verdi;P2",new(category));
        Assert.Equal(2,b.ValidRows);Assert.Equal(0,b.InvalidRows);Assert.Equal(before,await Digest(false));
        await Db(async db=>Assert.False(await db.Set<InstitutionEmailAssignment>().AnyAsync(a=>a.InstitutionId==_a)));
        await Expect(await Request(Imports+"/"+b.Id,await Access(b:true),host:host),HttpStatusCode.NotFound);
        var response=await ConfirmImport(host,token,b);await Expect(response,HttpStatusCode.OK);var result=(await response.Content.ReadFromJsonAsync<ImportResult>())!;Assert.Equal(2,result.CreatedCount);
        await Db(async db=>{Assert.Equal(users,await db.Users.LongCountAsync());Assert.Equal(members,await db.InstitutionMemberships.LongCountAsync());Assert.Equal(enrollments,await db.Set<StudentEnrollment>().LongCountAsync());Assert.Equal(subjects,await db.ClassSubjects.LongCountAsync());Assert.Equal(2,await db.Set<InstitutionImportItem>().CountAsync(i=>i.BatchId==b.Id));Assert.Null((await db.Set<InstitutionImportBatch>().SingleAsync(i=>i.Id==b.Id)).Payload);});
        await Db(async db=>{foreach(var item in result.Rows){if(item.StudentId is {} sid){Assert.False(await db.InstitutionStudentProfiles.AnyAsync(p=>p.StudentId==sid));Assert.False(await db.Set<StudentClassTransferEvent>().AnyAsync(e=>e.StudentId==sid));}if(item.TeacherId is {} tid)Assert.False(await db.InstitutionTeacherProfiles.AnyAsync(p=>p.TeacherId==tid));}});
        await Expect(await Request(Imports+"/"+b.Id+"/result",token,host:host),HttpStatusCode.OK);
    }
    [Theory]
    [InlineData("teacher")][InlineData("student")]
    public async Task BulkImport_LowRolesCannotUploadOrReadTemplates(string role)
    {await using var host=await ImportHost();var token=await Access(role);await Expect(await UploadImport(host,token,Encoding.UTF8.GetBytes("Nome;Cognome\nA;B"),new(ImportCategory.Student)),HttpStatusCode.Forbidden);await Expect(await Request(Imports+"/templates/students.csv",token,host:host),HttpStatusCode.Forbidden);}
    [Fact]
    public async Task BulkImport_InvalidRowBlocksEveryWriteWithStableRowErrors()
    {
        await using var host=await ImportHost();var token=await Access();var before=await Digest(false);var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi\nGiulia;");Assert.Equal(1,b.InvalidRows);
        var page=(await(await Request(Imports+"/"+b.Id+"/rows?page=1&size=1",token,host:host)).Content.ReadFromJsonAsync<ImportRowsPage>())!;Assert.Single(page.Items);Assert.Equal(2,page.TotalCount);
        await Expect(await ConfirmImport(host,token,b),HttpStatusCode.UnprocessableEntity);Assert.Equal(before,await Digest(false));
    }
    [Fact]
    public async Task BulkImport_InitialClassDoesNotCreateEnrollmentOrTransferAndForeignClassIsUnavailable()
    {
        await using var host=await ImportHost();var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome;Classe\nMario;Rossi;"+Id("class"));Assert.Equal(0,b.InvalidRows);
        var response=await ConfirmImport(host,token,b);await Expect(response,HttpStatusCode.OK);var result=(await response.Content.ReadFromJsonAsync<ImportResult>())!;
        await Db(async db=>{var id=result.Rows.Single().StudentId;Assert.Equal(Id("class"),(await db.Students.SingleAsync(s=>s.Id==id)).ClassId);Assert.False(await db.Set<StudentEnrollment>().AnyAsync(s=>s.StudentId==id));});
        var foreign=await PreviewImport(host,token,"Nome;Cognome;Classe\nMario;Rossi;"+Id("class",true));Assert.Equal(1,foreign.InvalidRows);await Expect(await ConfirmImport(host,token,foreign),HttpStatusCode.UnprocessableEntity);
    }
    [Fact]
    public async Task BulkImport_ReplayReturnsSameIdsAndSecondEventCannotRepeatCompletedWork()
    {
        await using var host=await ImportHost();var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi");var eventId=Guid.NewGuid();var a=await ConfirmImport(host,token,b,eventId);await Expect(a,HttpStatusCode.OK);
        var retry=await ConfirmImport(host,token,b,eventId);await Expect(retry,HttpStatusCode.OK);Assert.Equal(await a.Content.ReadAsStringAsync(),await retry.Content.ReadAsStringAsync());
        await Expect(await ConfirmImport(host,token,b),HttpStatusCode.Conflict);
        await Expect(await Request(Imports+"/"+b.Id+"/confirm",token,new ImportConfirmation(eventId,b.Revision,b.PreviewDigest,false),"POST",host),HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task BulkImport_EmailReservationUsesPhase2PolicyAndNumericSuffixInRowOrder()
    {
        await using var host=await ImportHost();var token=await Access();await PublishEmail(host,token,EmailConfig(collision:EmailCollisionStrategy.NumericSuffix));
        var b=await PreviewImport(host,await StaffToken(),"Nome;Cognome\nMario;Rossi\nMarco;Rossi",new(ImportCategory.Student,ImportEmailMode.ReserveInstitutionalAddress));
        var response=await ConfirmImport(host,await Access("teacher"),b);await Expect(response,HttpStatusCode.OK);
        await Db(async db=>{var addresses=await db.Set<InstitutionEmailAssignment>().Where(a=>a.InstitutionId==_a).OrderBy(a=>a.Email).ToListAsync();Assert.Equal(2,addresses.Count);Assert.Contains(addresses,a=>a.Email=="allievo_rossi.m@"+EmailConfig().Domain);Assert.Contains(addresses,a=>a.Email=="allievo_rossi.m_2@"+EmailConfig().Domain);Assert.All(addresses,a=>Assert.Equal("reserved",a.State));});
    }
    [Fact]
    public async Task BulkImport_TeacherEmailUsesItsOwnPublishedTemplateWithoutTeacherProfile()
    {
        await using var host=await ImportHost();var admin=await Access();await PublishEmail(host,admin,EmailConfig("docente_{nome}.{cognome}"),category:EmailCategory.Teacher);
        var token=await StaffToken();var b=await PreviewImport(host,token,"Nome;Cognome\nAnna Maria;Verdi",new(ImportCategory.Teacher,ImportEmailMode.ReserveInstitutionalAddress));
        var response=await ConfirmImport(host,token,b);await Expect(response,HttpStatusCode.OK);
        await Db(async db=>{var a=await db.Set<InstitutionEmailAssignment>().SingleAsync(a=>a.InstitutionId==_a);Assert.Equal("docente_annamaria.verdi@"+EmailConfig().Domain,a.Email);Assert.Null(a.StudentId);Assert.NotNull(a.TeacherId);Assert.Equal("reserved",a.State);Assert.False(await db.InstitutionTeacherProfiles.AnyAsync(p=>p.TeacherId==a.TeacherId));});
    }
    [Fact]
    public async Task BulkImport_ChangedPolicyInvalidatesConfirmationWithoutPartialRows()
    {
        await using var host=await ImportHost();var token=await Access();await PublishEmail(host,token);var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi",new(ImportCategory.Student,ImportEmailMode.ReserveInstitutionalAddress));
        await PublishEmail(host,token,EmailConfig("new_{nome}.{cognome}"),1);var before=await Digest(false);await Expect(await ConfirmImport(host,token,b),HttpStatusCode.Conflict);Assert.Equal(before,await Digest(false));
    }
    [Theory]
    [InlineData(ImportCategory.Student,false)][InlineData(ImportCategory.Student,true)][InlineData(ImportCategory.Teacher,false)][InlineData(ImportCategory.Teacher,true)]
    public async Task BulkImport_XlsxUsesSamePersistentWorkflow(ImportCategory category,bool staff)
    {
        await using var host=await ImportHost();var token=staff?await StaffToken():await Access();var bytes=InstitutionImportXlsxReaderTests.Workbook(new[]{new[]{"Nome","Cognome"},new[]{"José","García"}});
        var response=await UploadImport(host,token,bytes,new(category),"people.xlsx");await Expect(response,HttpStatusCode.Created);await Expect(await ConfirmImport(host,token,(await response.Content.ReadFromJsonAsync<ImportBatch>())!),HttpStatusCode.OK);
    }
}
