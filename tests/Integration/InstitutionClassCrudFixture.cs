using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Application.DependencyInjection;
using ElectronicRegisterAPI.Business.DependencyInjection;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;



using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.Controllers;
using ElectronicRegisterAPI.Infrastructure.Security;
namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed partial class InstitutionClassCrudTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private const string Password = "Mt3cTestPassword!";
    private static readonly string Hash = BCrypt.Net.BCrypt.HashPassword(Password);
    private readonly Guid _a=Guid.NewGuid(),_b=Guid.NewGuid(),_teacherUser=Guid.NewGuid(),_studentUser=Guid.NewGuid(),_adminUser=Guid.NewGuid(),_admin2=Guid.NewGuid();
    private readonly Dictionary<string,Guid[]> _ids=new();
    private Mt3cHosting.ProgramHost _host=null!;
    private readonly Guid _otherStudentUser = Guid.NewGuid();
    private Guid Id(string kind,bool b=false) { if(!_ids.TryGetValue(kind,out var ids))_ids[kind]=ids=new[]{Guid.NewGuid(),Guid.NewGuid()};return ids[b?1:0]; }
    private async Task Db(Func<ElectronicRegisterContext,Task> action)
    { using var scope=fixture.ScopeFactory.CreateScope();await action(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>()); }
    private async Task Execute(string sql,params (string,object)[] values)
    { await using var c=await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);SqlLockEvidence.Capture(c);await using var cmd=new MySqlCommand(sql,c);foreach(var (key,value) in values)cmd.Parameters.AddWithValue(key,value);await cmd.ExecuteNonQueryAsync(); }
    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);
        await Db(async db=>
        {
            foreach(var institution in new[]{_a,_b})db.Institutions.Add(new(){Id=institution,Code="MTCLASS_"+institution.ToString("N"),Name="Temporary ITS",Status="active"});
            await db.SaveChangesAsync();
            foreach(var isB in new[]{false,true})
            {
                var owner=isB?_b:_a;
                void Add<T>(T entity) where T:class {db.Add(entity);db.Entry(entity).Property<Guid>("InstitutionId").CurrentValue=owner;}
                Add(new E.Biennium{Id=Id("biennium",isB),StartYear=new DateOnly(2500,9,1),EndYear=new DateOnly(2502,8,31)});
                Add(new E.StudyArea{Id=Id("area",isB),Name="Same area",Description="Test"});
                Add(new E.StudyPath{Id=Id("path",isB),Name="Same path"});
                Add(new E.BienniumStudyArea{Id=Id("bsa",isB),BienniumId=Id("biennium",isB),StudyAreaId=Id("area",isB)});
                Add(new E.BienniumStudyPath{Id=Id("bsp",isB),BienniumStudyAreaId=Id("bsa",isB),StudyPathId=Id("path",isB)});
                foreach(var suffix in new[]{"","-other"})
                {
                    Add(new E.Class{Id=Id("class"+suffix,isB),BienniumStudyPathId=Id("bsp",isB),Name="Class"+suffix});
                    Add(new E.Teacher{Id=Id("teacher"+suffix,isB),FirstName="Teacher",LastName="Same"});
                    Add(new E.Student{Id=Id("student"+suffix,isB),FirstName="Student",LastName="Same",ClassId=Id("class"+suffix,isB)});
                    Add(new E.Subject{Id=Id("subject"+suffix,isB),Name=suffix==""?"Matematica":"Other subject"});
                    Add(new E.ClassSubject{Id=Id("assignment"+suffix,isB),ClassId=Id("class"+suffix,isB),TeacherId=Id("teacher"+suffix,isB),SubjectId=Id("subject"+suffix,isB)});
                }
                // Same subject also taught by another teacher in another class:
                // intersecting subject IDs alone would leak a by-teacher association.
                Add(new E.ClassSubject{Id=Id("assignment-shared",isB),ClassId=Id("class-other",isB),TeacherId=Id("teacher-other",isB),SubjectId=Id("subject",isB)});
            }
            db.Users.AddRange(new[]{_teacherUser,_studentUser,_adminUser,_admin2}.Select(id=>new E.User{Id=id,Email=id.ToString("N")+"@mtclass.example.test",PasswordHash=Hash,Role="student",TeacherId=null}));
            foreach(var isB in new[]{false,true})
            {
                foreach(var role in new[]{"admin","teacher","student"})
                {
                    var user=role=="admin"?_adminUser:role=="teacher"?_teacherUser:_studentUser;
                    var membership=Id(role+"-membership",isB);var owner=isB?_b:_a;
                    db.InstitutionMemberships.Add(new(){Id=membership,InstitutionId=owner,UserId=user,Status="active",InstitutionMembershipRoles=new List<E.InstitutionMembershipRole>{new(){MembershipId=membership,Role=role=="admin"?"InstitutionAdmin":role=="teacher"?"Teacher":"Student"}}});
                    if(role=="teacher")db.InstitutionTeacherProfiles.Add(new(){InstitutionId=owner,MembershipId=membership,TeacherId=Id("teacher",isB)});
                    if(role=="student")db.InstitutionStudentProfiles.Add(new(){InstitutionId=owner,MembershipId=membership,StudentId=Id("student",isB)});
                }
            }
            db.InstitutionMemberships.Add(new(){Id=Id("admin2-membership"),InstitutionId=_a,UserId=_admin2,Status="active",InstitutionMembershipRoles=new List<E.InstitutionMembershipRole>{new(){MembershipId=Id("admin2-membership"),Role="InstitutionAdmin"}}});
            await db.SaveChangesAsync();
        });
        _host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,exam:true,course:true);
    }

    private async Task<HttpResponseMessage> Request(string route, string? token = null, object? body = null, string method = "GET", Mt3cHosting.ProgramHost? host = null, string? etag = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await (host ?? _host).Client.SendAsync(request);
    }
    private async Task<string> Ticket(string role = "admin")
    {
        var user = role == "teacher" ? _teacherUser : role == "student" ? _studentUser : _adminUser;
        var response = await Request("/api/Auth/identity/login", body: new LoginDto { Email = user.ToString("N") + "@mtclass.example.test", Password = Password }, method: "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
    }
    private async Task<string> Access(string role = "admin", bool b = false)
    {
        var response = await Request("/api/Auth/select-institution", await Ticket(role), new SelectInstitutionDto { InstitutionId = b ? _b : _a, MembershipId = Id(role + "-membership", b) }, "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }
    private static string Route(string family) => "/api/institution/" + family + "s";
    private static object Input(string family, string name = "Created") => family == "subject" ? new { Name = name } : (object)new { FirstName = name, LastName = "Http" };
    private async Task<JsonArray> ArrayResponse(string route, string token)
    {
        var response = await Request(route, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonArray>())!;
    }
    private static Guid RowId(JsonNode? row) => row!["id"]!.GetValue<Guid>();
    private async Task<string> Digest(bool b)
    {
        await using var c = await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);
        var rows = new List<string>();
        foreach (var table in new[] { "student", "teacher", "subject", "class_subject", "grade", "exam", "exam_student", "exam_submission", "question", "question_option", "submission_answer", "exam_violation", "audit_event", "institution_student_profile", "institution_teacher_profile" })
        {
            await using var command = new MySqlCommand("SELECT * FROM `" + table + "` WHERE institution_id=@i ORDER BY 1", c);
            command.Parameters.AddWithValue("@i", b ? _b : _a);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { var cells = new object[reader.FieldCount]; reader.GetValues(cells); rows.Add(System.Text.Json.JsonSerializer.Serialize(cells)); }
        }
        return string.Join("\n", rows);
    }

    private static string Exams => "/api/institution/exams";
    private static string Submission(Guid id, string suffix) => "/api/institution/submissions/" + id + "/" + suffix;
    public async Task DisposeAsync() { if (_host is not null) await _host.DisposeAsync(); }
}
