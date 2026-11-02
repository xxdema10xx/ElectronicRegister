using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.DTOs.Email;
using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

internal static class Mysql84ReleaseHttp
{
    internal static async Task RunAsync(string evidence, string serverUuid)
    {
        using var configDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(evidence, "fresh-config.json")));
        var config = configDocument.RootElement;
        Assert.True(config.GetProperty("isolatedCI").GetBoolean());
        Assert.Equal("127.0.0.1", config.GetProperty("host").GetString());
        Assert.Equal("electronic_register_test", config.GetProperty("database").GetString());
        Assert.Equal(serverUuid, config.GetProperty("serverUuid").GetString());
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(evidence, "fresh-result.json")));
        var ids = result.RootElement.GetProperty("acceptance").GetProperty("ids");
        Guid Id(string key) => ids.GetProperty(key).GetGuid();
        var root = new MySqlConnectionStringBuilder { Server="127.0.0.1", Port=config.GetProperty("port").GetUInt32(), Database="electronic_register_test", UserID="root", SslMode=MySqlSslMode.Disabled };
        Assert.DoesNotContain(root.Port, new uint[] {3306,13306});
        await using var connection = new MySqlConnection(root.ConnectionString);
        await connection.OpenAsync();
        await using (var identity = new MySqlCommand("SELECT @@server_uuid", connection))
            Assert.Equal(serverUuid, await identity.ExecuteScalarAsync());
        const string password="Synthetic-http-only!2026";
        await using (var update = new MySqlCommand("UPDATE user SET password_hash=@hash WHERE id=@id",connection))
        {
            update.Parameters.AddWithValue("@hash", BCrypt.Net.BCrypt.HashPassword(password));
            update.Parameters.AddWithValue("@id", Id("user"));
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }
        // Only this attested loopback CI service uses an ephemeral RSA exchange.
        // Staging retains its existing VerifyFull TLS connection configuration.
        var api = new MySqlConnectionStringBuilder(root.ConnectionString) { UserID=config.GetProperty("apiUser").GetString(), Password="Synthetic-ci-only-credential", AllowPublicKeyRetrieval=true };
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(api.ConnectionString,Id("tenant"),[],enabled:false,school:true,course:true,exam:true,membershipAdmin:true,membershipWrite:true,onboarding:true,
            extraArguments:["--Deployment:LocalTestDatabasePort="+root.Port,"--Multitenancy:FormativePlans:Enabled=true","--Multitenancy:InstitutionEmail:Enabled=true","--Multitenancy:InstitutionImport:Enabled=true","--Multitenancy:InstitutionCalendar:Enabled=true"]);
        var login = await host.Client.PostAsJsonAsync("/api/Auth/identity/login",new LoginDto { Email=Id("user")+"@example.test", Password=password });
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var ticket=(await login.Content.ReadFromJsonAsync<IdentityTicketDto>())!;
        using var selection=new HttpRequestMessage(HttpMethod.Post,"/api/Auth/select-institution") { Content=JsonContent.Create(new SelectInstitutionDto { InstitutionId=Id("tenant"), MembershipId=Id("member") }) };
        selection.Headers.Authorization=new("Bearer",ticket.Ticket);
        var selected=await host.Client.SendAsync(selection);
        Assert.Equal(HttpStatusCode.OK,selected.StatusCode);
        var token=(await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        async Task<HttpResponseMessage> Send(HttpMethod method,string route,object? body=null)
        {
            using var message=new HttpRequestMessage(method,route);
            message.Headers.Authorization=new("Bearer",token);
            if(body is not null)message.Content=JsonContent.Create(body);
            return await host.Client.SendAsync(message);
        }
        foreach(var route in new[]{"/api/institution/formative-plans","/api/institution/email-policies","/api/institution/imports/templates/students.csv","/api/institution/imports/templates/teachers.csv","/api/institution/calendar/settings","/api/institution/admin/members"})
        {
            using var response=await Send(HttpMethod.Get,route);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        }
        foreach(var action in new[]{"Suspend","Reactivate"})
        {
            await using var revision=new MySqlCommand("SELECT security_generation FROM institution_membership_security_state WHERE membership_id=@id",connection);
            revision.Parameters.AddWithValue("@id",Id("target"));
            var body=new { EventId=Guid.NewGuid(), ExpectedRevision=Convert.ToInt64(await revision.ExecuteScalarAsync()), Action=action, Reason="Synthetic HTTP mutation" };
            using var response=await Send(HttpMethod.Post,"/api/institution/admin/members/"+Id("target")+"/mutations",body);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        }
        using(var created=await Send(HttpMethod.Post,"/api/institution/formative-plans",new CreateFormativePlan(Guid.NewGuid(),Id("path"),"HTTP","HTTP synthetic plan",null,"HTTP verification")))
            Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var template=new EmailTemplate("http_{nome}.{cognome}",Id("tenant").ToString("N")+".example.test",new());
        using(var policy=await Send(HttpMethod.Put,"/api/institution/email-policies/Student",new EmailPolicyWrite(Guid.NewGuid(),1,"Published",template,"HTTP verification")))
            Assert.Equal(HttpStatusCode.OK,policy.StatusCode);
        foreach(var category in new[]{ImportCategory.Student,ImportCategory.Teacher})
        {
            using var form=new MultipartFormDataContent();
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Nome;Cognome\nSynthetic;Http"+category)),"file","synthetic.csv");
            form.Add(new StringContent(JsonSerializer.Serialize(new ImportPreviewSettings(category))),"settings");
            using var request=new HttpRequestMessage(HttpMethod.Post,"/api/institution/imports/preview") { Content=form };
            request.Headers.Authorization=new("Bearer",token);
            using var response=await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created,response.StatusCode);
            var batch=(await response.Content.ReadFromJsonAsync<ImportBatch>())!;
            using var confirmed=await Send(HttpMethod.Post,"/api/institution/imports/"+batch.Id+"/confirm",new ImportConfirmation(Guid.NewGuid(),batch.Revision,batch.PreviewDigest,true,false,"HTTP verification"));
            Assert.Equal(HttpStatusCode.OK,confirmed.StatusCode);
        }
        // An institution token must never become a ticket for another tenant.
        using var foreign=await Send(HttpMethod.Post,"/api/Auth/select-institution",new SelectInstitutionDto { InstitutionId=Id("other"), MembershipId=Id("member") });
        Assert.Equal(HttpStatusCode.Unauthorized,foreign.StatusCode);
        using var crossTenantRequest=new HttpRequestMessage(HttpMethod.Post,"/api/Auth/select-institution") { Content=JsonContent.Create(new SelectInstitutionDto { InstitutionId=Id("other"), MembershipId=Id("member") }) };
        crossTenantRequest.Headers.Authorization=new("Bearer",ticket.Ticket);
        using var crossTenant=await host.Client.SendAsync(crossTenantRequest);
        Assert.Equal(HttpStatusCode.Forbidden,crossTenant.StatusCode);
        await File.WriteAllTextAsync(Path.Combine(evidence,"http-result.json"),JsonSerializer.Serialize(new { realHttp=true, leastPrivilegeApi=true, membershipMutations=true, emailPolicy=true, studentTeacherImports=true, formativePlan=true, calendarRead=true, foreignTenantRejected=true }));
    }
}
