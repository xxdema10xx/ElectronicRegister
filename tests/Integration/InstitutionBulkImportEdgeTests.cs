using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Fact]
    public async Task BulkImport_PendingQuotaAndRuntimeRolloutAreRevalidated()
    {
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,
            extraArguments:new[]{"--Multitenancy:InstitutionImport:Enabled=true","--Multitenancy:InstitutionImport:MaxPendingPreviews=1"});
        var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi");
        await Expect(await UploadImport(host,token,Encoding.UTF8.GetBytes("Nome;Cognome\nAnna;Verdi"),new(ImportCategory.Student)),HttpStatusCode.Conflict);
        host.Host.Services.GetRequiredService<IConfiguration>()["Multitenancy:InstitutionImport:Enabled"]="false";
        await Expect(await ConfirmImport(host,token,b),HttpStatusCode.Forbidden);await Db(async db=>Assert.False(await db.Set<InstitutionImportItem>().AnyAsync(i=>i.BatchId==b.Id)));
    }
    [Fact]
    public async Task BulkImport_IdenticalRowsRequireWarningAcknowledgementButRemainDistinctPeople()
    {
        await using var host=await ImportHost();var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome\nMario;Rossi\nMario;Rossi");Assert.Equal(2,b.ValidRows);
        await Expect(await ConfirmImport(host,token,b,acknowledge:false),HttpStatusCode.Conflict);var response=await ConfirmImport(host,token,b);await Expect(response,HttpStatusCode.OK);
        Assert.Equal(2,(await response.Content.ReadFromJsonAsync<ImportResult>())!.Rows.Select(i=>i.StudentId).Distinct().Count());
    }
    [Theory]
    [InlineData(ImportCategory.Student)][InlineData(ImportCategory.Teacher)]
    public async Task BulkImport_BoundedManyRowBatchCreatesEveryTargetInOneCommit(ImportCategory category)
    {
        await using var host=await ImportHost();var token=await StaffToken();var csv="Nome;Cognome;RiferimentoEsterno\n"+string.Join("\n",Enumerable.Range(1,100).Select(i=>"Import"+i+";Person"+i+";REF-"+i));var b=await PreviewImport(host,token,csv,new(category));Assert.Equal(100,b.ValidRows);
        var response=await ConfirmImport(host,token,b);await Expect(response,HttpStatusCode.OK);var result=(await response.Content.ReadFromJsonAsync<ImportResult>())!;Assert.Equal(100,result.CreatedCount);Assert.Equal(100,result.Rows.Select(r=>r.StudentId??r.TeacherId).Distinct().Count());
        await Db(async db=>Assert.Equal(100,await db.Set<InstitutionImportItem>().CountAsync(i=>i.BatchId==b.Id)));
    }
    [Theory]
    [InlineData("oversize",413)][InlineData("rows",413)][InlineData("format",415)][InlineData("formula",400)]
    public async Task BulkImport_HostileUploadsDoNotCreateStagingOrPeople(string kind,int status)
    {
        await using var host=await ImportHost();var token=await Access();byte[] bytes;var name="people.csv";
        if(kind=="oversize")bytes=new byte[10*1024*1024+1];
        else if(kind=="rows")bytes=Encoding.UTF8.GetBytes("Nome;Cognome\n"+string.Join("\n",Enumerable.Repeat("Mario;Rossi",2001)));
        else if(kind=="format"){bytes=new byte[]{1,2,3};name="people.xlsm";}
        else{bytes=InstitutionImportXlsxReaderTests.Workbook(new[]{new[]{"Nome","Cognome"},new[]{"Mario","Rossi"}},formula:true);name="people.xlsx";}
        var before=await Digest(false);await Expect(await UploadImport(host,token,bytes,new(ImportCategory.Student),name),(HttpStatusCode)status);Assert.Equal(before,await Digest(false));await Db(async db=>Assert.False(await db.Set<InstitutionImportBatch>().AnyAsync(i=>i.InstitutionId==_a)));
    }
    [Fact]
    public async Task BulkImport_MalformedMappingAndForbiddenDestinationsAreRejectedBeforeStaging()
    {
        await using var host=await ImportHost();var token=await Access();
        foreach(var json in new[]{"{","{\"category\":\"Student\",\"mapping\":[{\"columnIndex\":0,\"field\":\"Role\"}]}","{\"category\":\"Student\",\"institutionId\":\"forged\"}"})
        {
            using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Nome;Cognome\nMario;Rossi")),"file","people.csv");form.Add(new StringContent(json),"settings");using var request=new HttpRequestMessage(HttpMethod.Post,Imports+"/preview"){Content=form};request.Headers.Authorization=new("Bearer",token);await Expect(await host.Client.SendAsync(request),HttpStatusCode.BadRequest);
        }
        await Db(async db=>Assert.False(await db.Set<InstitutionImportBatch>().AnyAsync(b=>b.InstitutionId==_a)));
    }
    [Fact]
    public async Task BulkImport_LegacyTokenAndDirectManagerWithoutCurrentTenantFailClosed()
    {
        await using var host=await ImportHost();var jwt=new JwtSecurityToken("mt2-tests","mt2-client",new[]{new Claim("sub",_adminUser.ToString()),new Claim(ClaimTypes.Role,"admin")},expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Mt3cHosting.Key)),SecurityAlgorithms.HmacSha256));
        await Expect(await UploadImport(host,new JwtSecurityTokenHandler().WriteToken(jwt),Encoding.UTF8.GetBytes("Nome;Cognome\nMario;Rossi"),new(ImportCategory.Student)),HttpStatusCode.Unauthorized);
        using var scope=host.Host.Services.CreateScope();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>scope.ServiceProvider.GetRequiredService<IInstitutionImportManager>().TemplateAsync(ImportCategory.Student,default));
    }
    [Fact]
    public async Task BulkImport_DatabaseGuardsProtectHistoryAndCrossTenantLinks()
    {
        await using var host=await ImportHost();var token=await Access();var b=await PreviewImport(host,token,"Nome;Cognome;RiferimentoEsterno\nMario;Rossi;REF-1");await Expect(await ConfirmImport(host,token,b),HttpStatusCode.OK);
        foreach(var sql in new[]{"DELETE FROM institution_import_batch WHERE id=@id","DELETE FROM institution_import_item WHERE batch_id=@id","UPDATE institution_import_event SET reason='tamper' WHERE batch_id=@id","DELETE FROM institution_import_external_reference WHERE institution_id=@tenant"})
            Assert.Equal(1644,(await Assert.ThrowsAsync<MySqlException>(()=>Execute(sql,("@id",b.Id),("@tenant",_a)))).Number);
        var fk=await Assert.ThrowsAsync<MySqlException>(()=>Execute("INSERT INTO institution_import_external_reference(institution_id,category,reference,student_id) VALUES(@t,'Student','foreign',@s)",("@t",_a),("@s",Id("student",true))));Assert.Equal(1452,fk.Number);
    }
}
