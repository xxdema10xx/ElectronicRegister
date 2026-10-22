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
public sealed class Mt3cHttpTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private const string Password = "Mt3cTestPassword!";
    private static readonly string Hash = BCrypt.Net.BCrypt.HashPassword(Password);
    private readonly Guid _a=Guid.NewGuid(),_b=Guid.NewGuid(),_teacherUser=Guid.NewGuid(),_studentUser=Guid.NewGuid(),_adminUser=Guid.NewGuid(),_admin2=Guid.NewGuid();
    private readonly Dictionary<string,Guid[]> _ids=new();
    private Mt3cHosting.ProgramHost _host=null!;
    private Guid Id(string kind,bool b=false) { if(!_ids.TryGetValue(kind,out var ids))_ids[kind]=ids=new[]{Guid.NewGuid(),Guid.NewGuid()};return ids[b?1:0]; }
    private async Task Db(Func<ElectronicRegisterContext,Task> action)
    { using var scope=fixture.ScopeFactory.CreateScope();await action(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>()); }
    private async Task Execute(string sql,params (string,object)[] values)
    { await using var c=await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);await using var cmd=new MySqlCommand(sql,c);foreach(var (key,value) in values)cmd.Parameters.AddWithValue(key,value);await cmd.ExecuteNonQueryAsync(); }
    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);
        await Db(async db=>
        {
            foreach(var institution in new[]{_a,_b})db.Institutions.Add(new(){Id=institution,Code="MT3C_"+institution.ToString("N"),Name="Temporary ITS",Status="active"});
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
            db.Users.AddRange(new[]{_teacherUser,_studentUser,_adminUser,_admin2}.Select(id=>new E.User{Id=id,Email=id.ToString("N")+"@mt3c.example.test",PasswordHash=Hash,Role=id==_teacherUser?"admin":"student",TeacherId=id==_teacherUser?Id("teacher"):null}));
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
        _host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true);
    }

    private async Task<HttpResponseMessage> Request(string route, string? token = null, object? body = null, string method = "GET", Mt3cHosting.ProgramHost? host = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await (host ?? _host).Client.SendAsync(request);
    }
    private async Task<string> Ticket(string role = "admin")
    {
        var user = role == "teacher" ? _teacherUser : role == "student" ? _studentUser : _adminUser;
        var response = await Request("/api/Auth/identity/login", body: new LoginDto { Email = user.ToString("N") + "@mt3c.example.test", Password = Password }, method: "POST");
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
        foreach (var table in new[] { "student", "teacher", "subject", "class_subject", "grade", "institution_student_profile", "institution_teacher_profile" })
        {
            await using var command = new MySqlCommand("SELECT * FROM `" + table + "` WHERE institution_id=@i ORDER BY 1,2", c);
            command.Parameters.AddWithValue("@i", b ? _b : _a);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { var cells = new object[reader.FieldCount]; reader.GetValues(cells); rows.Add(System.Text.Json.JsonSerializer.Serialize(cells)); }
        }
        return string.Join("\n", rows);
    }

    [Fact]
    public async Task StudentReadsRespectAdminTeacherAndOwnProfileAcrossBothTenants()
    {
        foreach (var b in new[] { false, true })
        foreach (var role in new[] { "admin", "teacher" })
        {
            var token = await Access(role, b);
            var route = Route("student");
            var expected = role == "admin" ? 2 : 1;
            var rows = await ArrayResponse(route, token);
            Assert.Equal(expected, rows.Count);
            Assert.Contains(Id("student", b), rows.Select(RowId));
            Assert.DoesNotContain(Id("student", !b), rows.Select(RowId));
            Assert.Equal(expected, await (await Request(route + "/count", token)).Content.ReadFromJsonAsync<int>());
            Assert.Equal(expected, (await ArrayResponse(route + "/bylastname/Same", token)).Count);
            Assert.Empty(await ArrayResponse(route + "/bylastname/Nobody", token));
            Assert.Equal(HttpStatusCode.OK, (await Request(route + "/" + Id("student", b), token)).StatusCode);
            if (role == "teacher") Assert.Equal(HttpStatusCode.NotFound, (await Request(route + "/" + Id("student-other", b), token)).StatusCode);
        }
        foreach (var b in new[] { false, true })
        {
            var token = await Access("student", b);
            var response = await Request(Route("student") + "/me?studentId=" + Id("student", !b), token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(Id("student", b), RowId(await response.Content.ReadFromJsonAsync<JsonObject>()));
            Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route("student"), token)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route("student") + "/count", token)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route("student") + "/" + Id("student", b), token)).StatusCode);
        }
    }

    [Theory]
    [InlineData("student")]
    [InlineData("teacher")]
    [InlineData("subject")]
    public async Task AdminCrudReturnsRealLocationAndPersistsOnlyOwnTenant(string family)
    {
        var token = await Access();
        var beforeB = await Digest(true);
        var route = Route(family);
        var created = await Request(route, token, Input(family), "POST");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Contains(route + "/" + id, created.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await Request(created.Headers.Location!.ToString(), token)).StatusCode);
        await Db(async db =>
        {
            var sql = "SELECT institution_id AS Value FROM `" + family + "` WHERE id={0}";
            Assert.Equal(_a, await db.Database.SqlQueryRaw<Guid>(sql, id).SingleAsync());
        });
        Assert.Equal(HttpStatusCode.NoContent, (await Request(route + "/" + id, token, Input(family, "Updated"), "PUT")).StatusCode);
        var updated = await Request(route + "/" + id, token);
        var row = await updated.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("Updated", row![family == "subject" ? "name" : "firstName"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await Request(route + "/" + id, token, method: "DELETE")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Request(route + "/" + id, token)).StatusCode);
        Assert.Equal(beforeB, await Digest(true));
        await Db(async db => Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM `" + family + "` WHERE id={0}", id).SingleAsync()));
    }

    [Theory]
    [InlineData("student", "GET")]
    [InlineData("student", "PUT")]
    [InlineData("student", "DELETE")]
    [InlineData("teacher", "GET")]
    [InlineData("teacher", "PUT")]
    [InlineData("teacher", "DELETE")]
    [InlineData("subject", "GET")]
    [InlineData("subject", "PUT")]
    [InlineData("subject", "DELETE")]
    public async Task CrossTenantIdsAreIndistinguishableFromMissingAndNeverChangePersistedData(string family, string method)
    {
        var token = await Access();
        var a = await Digest(false); var b = await Digest(true);
        var foreign = await Request(Route(family) + "/" + Id(family, true), token, method == "PUT" ? Input(family) : null, method);
        var missing = await Request(Route(family) + "/" + Guid.NewGuid(), token, method == "PUT" ? Input(family) : null, method);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(foreign.StatusCode, missing.StatusCode);
        // MVC NotFound and handled exceptions may differ in detail presence, but never in title or status.
        Assert.Equal((await foreign.Content.ReadFromJsonAsync<JsonObject>())!["title"]!.ToString(), (await missing.Content.ReadFromJsonAsync<JsonObject>())!["title"]!.ToString());
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
    }

    [Theory]
    [InlineData("student", "teacher")]
    [InlineData("student", "student")]
    [InlineData("teacher", "teacher")]
    [InlineData("teacher", "student")]
    [InlineData("subject", "teacher")]
    [InlineData("subject", "student")]
    public async Task NonAdministratorsNeverWriteAndDatabaseRemainsUnchanged(string family, string role)
    {
        var token = await Access(role); var a = await Digest(false); var b = await Digest(true);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route(family), token, Input(family), "POST")).StatusCode);
        foreach (var method in new[] { "PUT", "DELETE" })
            Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route(family) + "/" + Id(family), token, method == "PUT" ? Input(family) : null, method)).StatusCode);
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
    }

    [Theory]
    [InlineData("student", "class")]
    [InlineData("student", "membership")]
    [InlineData("teacher", "membership")]
    public async Task ForeignLinksAreRejectedAtomicallyByManager(string family, string link)
    {
        var a = await Digest(false); var b = await Digest(true);
        var query = link == "class" ? "?classId=" + Id("class", true) : "?membershipId=" + Id(family + "-membership", true);
        Assert.Equal(HttpStatusCode.NotFound, (await Request(Route(family) + query, await Access(), Input(family), "POST")).StatusCode);
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
    }

    [Theory]
    [InlineData("student")]
    [InlineData("teacher")]
    public async Task MembershipLinksNeedExistingActiveRoleAndUniqueProfile(string family)
    {
        var token = await Access(); var a = await Digest(false);
        var route = Route(family);
        Assert.Equal(HttpStatusCode.NotFound, (await Request(route + "?membershipId=" + Id("admin-membership"), token, Input(family), "POST")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Request(route + "?membershipId=" + Id(family + "-membership"), token, Input(family), "POST")).StatusCode);
        await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@m", ("@m", Id(family + "-membership")));
        Assert.Equal(HttpStatusCode.NotFound, (await Request(route + "?membershipId=" + Id(family + "-membership"), token, Input(family), "POST")).StatusCode);
        Assert.Equal(a, await Digest(false));
    }

    [Fact]
    public async Task TeacherDirectoryAndSelfProfileAreLocalForEveryRole()
    {
        foreach (var b in new[] { false, true })
        foreach (var role in new[] { "admin", "teacher", "student" })
        {
            var token = await Access(role, b); var route = Route("teacher");
            Assert.Equal(new[] { Id("teacher", b), Id("teacher-other", b) }.Order(), (await ArrayResponse(route, token)).Select(RowId).Order());
            Assert.Equal(2, (await ArrayResponse(route + "/bylastname/Same", token)).Count);
            Assert.Empty(await ArrayResponse(route + "/bylastname/Nobody", token));
            Assert.Equal(HttpStatusCode.OK, (await Request(route + "/" + Id("teacher", b), token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Request(route + "/" + Id("teacher", !b), token)).StatusCode);
            var count = await Request(route + "/count", token);
            Assert.Equal(role == "student" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, count.StatusCode);
            if (role != "student") Assert.Equal(2, await count.Content.ReadFromJsonAsync<int>());
            var me = await Request(route + "/me?teacherId=" + Id("teacher", !b), token);
            Assert.Equal(role == "teacher" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, me.StatusCode);
            if (role == "teacher") Assert.Equal(Id("teacher", b), RowId(await me.Content.ReadFromJsonAsync<JsonObject>()));
        }
    }

    [Fact]
    public async Task SubjectsIntersectAssignmentsForAllLookupsIncludingByTeacher()
    {
        foreach (var b in new[] { false, true })
        foreach (var role in new[] { "admin", "teacher", "student" })
        {
            var token = await Access(role, b); var route = Route("subject"); var expected = role == "admin" ? 2 : 1;
            var rows = await ArrayResponse(route, token);
            Assert.Equal(expected, rows.Count); Assert.Contains(Id("subject", b), rows.Select(RowId));
            Assert.Equal(expected, await (await Request(route + "/count", token)).Content.ReadFromJsonAsync<int>());
            foreach (var suffix in new[] { "/" + Id("subject", b), "/byname/Matematica" })
            {
                var response = await Request(route + suffix, token); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(Id("subject", b), RowId(await response.Content.ReadFromJsonAsync<JsonObject>()));
            }
            Assert.Equal(HttpStatusCode.NotFound, (await Request(route + "/" + Id("subject", !b), token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Request(route + "/byname/Missing", token)).StatusCode);
            Assert.Empty(await ArrayResponse(route + "/byteacher/" + Id("teacher", !b), token));
            Assert.Single(await ArrayResponse(route + "/byteacher/" + Id("teacher", b), token));
            var other = await ArrayResponse(route + "/byteacher/" + Id("teacher-other", b), token);
            Assert.Equal(role == "admin" ? 2 : 0, other.Count);
        }
    }

    [Fact]
    public async Task UnassignedTeacherAndStudentWithoutClassReceiveEmptyCollections()
    {
        await Execute("DELETE FROM class_subject WHERE teacher_id=@t", ("@t", Id("teacher")));
        var teacher = await Access("teacher");
        foreach (var family in new[] { "student", "subject" })
        {
            Assert.Empty(await ArrayResponse(Route(family), teacher));
            Assert.Equal(0, await (await Request(Route(family) + "/count", teacher)).Content.ReadFromJsonAsync<int>());
        }
        await Execute("UPDATE student SET class_id=NULL WHERE id=@s", ("@s", Id("student")));
        var student = await Access("student");
        Assert.Empty(await ArrayResponse(Route("subject"), student));
        Assert.Empty(await ArrayResponse(Route("subject") + "/byteacher/" + Id("teacher-other"), student));
    }

    [Fact]
    public async Task NamesAreUniqueOnlyWithinInstitutionAndDependenciesRemainConflicts()
    {
        var token = await Access();
        Assert.Equal(HttpStatusCode.Conflict, (await Request(Route("subject"), token, new { Name = "Matematica" }, "POST")).StatusCode);
        var name = "Shared HTTP " + Guid.NewGuid().ToString("N");
        foreach (var b in new[] { false, true })
            Assert.Equal(HttpStatusCode.Created, (await Request(Route("subject"), await Access(b: b), new { Name = name }, "POST")).StatusCode);
        foreach (var family in new[] { "teacher", "subject" })
            Assert.Equal(HttpStatusCode.Conflict, (await Request(Route(family) + "/" + Id(family), token, method: "DELETE")).StatusCode);
        await Execute("INSERT INTO grade(id,institution_id,student_id,class_subject_id,value,date) VALUES (@id,@i,@s,@cs,8,'2500-10-01')", ("@id", Guid.NewGuid()), ("@i", _a), ("@s", Id("student")), ("@cs", Id("assignment")));
        var before = await Digest(false);
        Assert.Equal(HttpStatusCode.Conflict, (await Request(Route("student") + "/" + Id("student"), token, method: "DELETE")).StatusCode);
        Assert.Equal(before, await Digest(false));
    }

    private string Alter(string token, string fault)
    {
        var pieces = token.Split('.');
        var payload = JsonNode.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(pieces[1]))!.AsObject();
        switch (fault)
        {
            case "expired": payload["exp"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(); payload["nbf"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(); break;
            case "audience": payload["aud"] = "wrong-audience"; break;
            case "other-user": payload["sub"] = _teacherUser.ToString(); break;
            case "legacy": payload["aud"] = "mt2-client"; payload.Remove("purpose"); payload.Remove("institution_id"); payload.Remove("membership_id"); payload.Remove("institution_role"); payload["role"] = "admin"; break;
        }
        if (fault == "legacy")
        {
            var header = JsonNode.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(pieces[0]))!.AsObject();
            header["typ"] = "JWT"; pieces[0] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(header.ToJsonString());
        }
        pieces[1] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(payload.ToJsonString());
        var input = pieces[0] + "." + pieces[1];
        return input + "." + Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Mt3cHosting.Key), System.Text.Encoding.UTF8.GetBytes(input)));
    }

    private IEnumerable<(string Method, string Path)> SchoolActions()
    {
        foreach (var family in new[] { "student", "teacher", "subject" })
        {
            var route = Route(family);
            yield return ("GET", route); yield return ("GET", route + "/count"); yield return ("GET", route + "/" + Id(family));
            yield return ("POST", route); yield return ("PUT", route + "/" + Id(family)); yield return ("DELETE", route + "/" + Id(family));
            yield return ("GET", route + (family == "subject" ? "/byname/Matematica" : "/bylastname/Same"));
            yield return ("GET", route + (family == "subject" ? "/byteacher/" + Id("teacher") : "/me"));
        }
    }

    [Theory]
    [InlineData("none", 401)]
    [InlineData("legacy", 401)]
    [InlineData("selection", 401)]
    [InlineData("expired", 401)]
    [InlineData("audience", 401)]
    [InlineData("other-user", 403)]
    public async Task EveryActionRejectsInvalidTokenBeforeAnyWrite(string fault, int expected)
    {
        var original = await Access();
        var token = fault == "none" ? null : fault == "selection" ? await Ticket() : Alter(original, fault);
        var a = await Digest(false); var b = await Digest(true);
        foreach (var (method, path) in SchoolActions())
        {
            var response = await Request(path, token, method is "POST" or "PUT" ? new { FirstName = "Forbidden", LastName = "Write", Name = "Forbidden" } : null, method);
            Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        }
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("institution")]
    [InlineData("admin-removed")]
    [InlineData("teacher-removed")]
    [InlineData("roles-added")]
    public async Task CommittedRevocationBlocksAllActionsAndWrites(string fault)
    {
        var token = await Access(fault == "teacher-removed" ? "teacher" : "admin");
        if (fault == "membership") await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id", ("@id", Id("admin-membership")));
        else if (fault == "institution") await Execute("UPDATE institution SET status='suspended' WHERE id=@id", ("@id", _a));
        else if (fault == "roles-added") await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@id,'Teacher')", ("@id", Id("admin-membership")));
        else await Execute("DELETE FROM institution_membership_role WHERE membership_id=@id", ("@id", Id(fault == "teacher-removed" ? "teacher-membership" : "admin-membership")));
        var a = await Digest(false); var b = await Digest(true);
        foreach (var (method, path) in SchoolActions())
            Assert.Equal(HttpStatusCode.Forbidden, (await Request(path, token, method is "POST" or "PUT" ? new { Name = "Revoked", FirstName = "Revoked", LastName = "Write" } : null, method)).StatusCode);
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
        Assert.Equal(HttpStatusCode.OK, (await Request(Route("student"), await Access(b: true))).StatusCode);
    }

    [Fact]
    public async Task MultipleRolesStayWithinSelectedMembershipAndProfilesNeverFallBackToUser()
    {
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@id,'InstitutionAdmin')", ("@id", Id("teacher-membership")));
        var a = await Access("teacher"); var b = await Access("teacher", true);
        Assert.Equal(2, (await ArrayResponse(Route("student"), a)).Count);
        Assert.Single(await ArrayResponse(Route("student"), b));
        Assert.Equal(HttpStatusCode.Created, (await Request(Route("subject"), a, new { Name = "Multirole" }, "POST")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route("subject"), b, new { Name = "Forbidden" }, "POST")).StatusCode);
        await Execute("DELETE FROM institution_teacher_profile WHERE membership_id=@id", ("@id", Id("teacher-membership", true)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route("teacher") + "/me", b)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route("subject"), b)).StatusCode);
        await Execute("DELETE FROM institution_student_profile WHERE membership_id=@id", ("@id", Id("student-membership")));
        Assert.Equal(HttpStatusCode.Forbidden, (await Request(Route("student") + "/me", await Access("student"))).StatusCode);
    }

    [Fact]
    public async Task DefaultRolloutRejectsValidScopedTokensForAllActions()
    {
        var token = await Access(); var before = await Digest(false);
        await using var closed = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false);
        foreach (var (method, path) in SchoolActions())
            Assert.Equal(HttpStatusCode.Forbidden, (await Request(path, token, method is "POST" or "PUT" ? Input("student") : null, method, closed)).StatusCode);
        Assert.Equal(before, await Digest(false));
    }

    private static string EvidenceDirectory()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/Integration/TestResults/mt3c"));
        Directory.CreateDirectory(path); return path;
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task RealBootstrapHasExactPoliciesUnambiguousRoutesOpenApiAndWorker(string environment)
    {
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, environment: environment);
        var token = await Access();
        Assert.Equal(HttpStatusCode.OK, (await Request(Route("student"), token, host: host)).StatusCode);
        var provider = host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>();
        var policy = await provider.GetPolicyAsync(InstitutionSchoolEndpointAttribute.PolicyName);
        Assert.Equal(new[] { InstitutionTokenContract.AccessScheme }, policy!.AuthenticationSchemes);
        var endpoints = host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is not null).ToArray();
        var rows = endpoints.SelectMany(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods.Select(method =>
        {
            var action = e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()!;
            var auth = e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
            return new { method, route = e.RoutePattern.RawText, controller = action.ControllerName, action = action.ActionName,
                roles = auth.Select(a => a.Roles).Where(r => !string.IsNullOrEmpty(r)).ToArray(), policies = auth.Select(a => a.Policy).Where(p => !string.IsNullOrEmpty(p)).ToArray(),
                migrated = e.Metadata.GetMetadata<InstitutionSchoolEndpointAttribute>() is not null,
                managers = action.ControllerTypeInfo.DeclaredConstructors.SelectMany(c => c.GetParameters()).Select(p => p.ParameterType.Name).ToArray() };
        })).ToArray();
        Assert.Equal(rows.Length, rows.Select(r => r.method + " " + r.route).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var school = rows.Where(r => r.migrated).ToArray();
        Assert.Equal(52, school.Length);
        Assert.All(school, r =>
        {
            Assert.Equal(new[] { InstitutionSchoolEndpointAttribute.PolicyName }, r.policies);
            Assert.Single(r.roles);
            Assert.Single(r.managers); Assert.StartsWith("IInstitution", r.managers[0]);
            Assert.DoesNotContain("institutionId", r.route!, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(rows.Where(r => !r.migrated && !r.controller.Equals("InstitutionIdentity")), r => Assert.DoesNotContain(InstitutionSchoolEndpointAttribute.PolicyName, r.policies));
        await File.WriteAllTextAsync(Path.Combine(EvidenceDirectory(), "routes-" + environment + ".json"), System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        var openApi = await host.Client.GetAsync("/openapi/v1.json");
        Assert.Equal(environment == "Development" ? HttpStatusCode.OK : HttpStatusCode.NotFound, openApi.StatusCode);
        if (environment == "Development")
        {
            var document = (await openApi.Content.ReadFromJsonAsync<JsonObject>())!;
            var paths = document["paths"]!.AsObject();
            foreach (var row in school)
            {
                var path = "/" + row.route!.Replace(":guid", "");
                Assert.NotNull(paths[path]?[row.method.ToLowerInvariant()]);
            }
            await File.WriteAllTextAsync(Path.Combine(EvidenceDirectory(), "openapi-school-paths.json"), new JsonObject(paths.Where(p => p.Key.StartsWith("/api/institution/")).Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, p.Value!.DeepClone()))).ToJsonString());
        }
        // Host helper asserts real worker registration and orderly ApplicationStopped on disposal.
    }

    [Fact]
    public async Task HttpQueriesKeepTenantSqlServerCountAndConstantListCost()
    {
        using var capture = new SqlObserver();
        using var subscription = DiagnosticListener.AllListeners.Subscribe(capture);
        var observations = new List<object>();
        foreach (var role in new[] { "admin", "teacher", "student" })
        {
            var token = await Access(role);
            foreach (var family in role == "student" ? new[] { "subject", "teacher" } : new[] { "student", "teacher", "subject" })
            foreach (var operation in new[] { "list", "detail", "count" })
            {
                if (role == "student" && family == "teacher" && operation == "count") continue;
                var path = Route(family) + (operation == "detail" ? "/" + Id(family) : operation == "count" ? "/count" : "");
                capture.Commands.Clear(); var watch = Stopwatch.StartNew();
                var response = await Request(path, token); watch.Stop();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var sql = capture.Commands.ToArray();
                Assert.InRange(sql.Length, 3, 8);
                var resource = sql.Last(s => s.Contains("FROM `" + family + "`", StringComparison.Ordinal));
                Assert.Contains("institution_id", resource);
                if (operation == "count") Assert.Contains("COUNT(*)", resource);
                if (family == "student" && role == "teacher" || family == "subject" && role != "admin") Assert.Contains("EXISTS", resource, StringComparison.OrdinalIgnoreCase);
                observations.Add(new { role, family, operation, queries = sql.Length, milliseconds = watch.Elapsed.TotalMilliseconds, sql });
            }
        }
        foreach (var role in new[] { "student", "teacher" })
        {
            var token = await Access(role); capture.Commands.Clear();
            Assert.Equal(HttpStatusCode.OK, (await Request(Route(role) + "/me", token)).StatusCode);
            Assert.InRange(capture.Commands.Count, 3, 8);
            observations.Add(new { role, operation = "me", queries = capture.Commands.Count, sql = capture.Commands.ToArray() });
        }
        // Adding rows changes result size, not the number of round trips.
        var admin = await Access(); capture.Commands.Clear(); await ArrayResponse(Route("student"), admin); var baseline = capture.Commands.Count;
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Created, (await Request(Route("student"), admin, Input("student", "Extra" + i), "POST")).StatusCode);
        capture.Commands.Clear(); Assert.Equal(7, (await ArrayResponse(Route("student"), admin)).Count); Assert.Equal(baseline, capture.Commands.Count);
        await File.WriteAllTextAsync(Path.Combine(EvidenceDirectory(), "http-query-observations.json"), System.Text.Json.JsonSerializer.Serialize(observations, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class SqlObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        public ConcurrentQueue<string> Commands { get; } = new();
        public void OnNext(DiagnosticListener listener) { if (listener.Name == "Microsoft.EntityFrameworkCore") _subscriptions.Add(listener.Subscribe(this)); }
        public void OnNext(KeyValuePair<string, object?> item)
        {
            if (item.Value is CommandExecutedEventData data) Commands.Enqueue(data.Command.CommandText);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
        public void Dispose() { foreach (var subscription in _subscriptions) subscription.Dispose(); }
    }

    [Theory]
    [InlineData("student")]
    [InlineData("teacher")]
    [InlineData("subject")]
    public async Task SearchAndCountNeverRevealForeignOnlyNames(string family)
    {
        var b = await Access(b: true); var a = await Access();
        var name = "OnlyB" + Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.NoContent, (await Request(Route(family) + "/" + Id(family, true), b, family == "subject" ? new { Name = name } : (object)new { FirstName = "B", LastName = name }, "PUT")).StatusCode);
        var search = Route(family) + (family == "subject" ? "/byname/" : "/bylastname/") + name;
        if (family == "subject") Assert.Equal(HttpStatusCode.NotFound, (await Request(search, a)).StatusCode);
        else Assert.Empty(await ArrayResponse(search, a));
        Assert.Equal(2, await (await Request(Route(family) + "/count", a)).Content.ReadFromJsonAsync<int>());
    }

    [Fact]
    public async Task ClientInstitutionIdentifiersCannotSelectOwnerOrContext()
    {
        var token = await Access(); var beforeB = await Digest(true);
        using var request = new HttpRequestMessage(HttpMethod.Post, Route("student") + "?institutionId=" + _b);
        request.Headers.Authorization = new("Bearer", token); request.Headers.Add("X-Institution-Id", _b.ToString());
        request.Content = JsonContent.Create(new { FirstName = "Scoped", LastName = "Owner", InstitutionId = _b });
        var response = await _host.Client.SendAsync(request); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        await Db(async db => Assert.Equal(_a, await db.Database.SqlQueryRaw<Guid>("SELECT institution_id AS Value FROM student WHERE id={0}", id).SingleAsync()));
        Assert.Equal(beforeB, await Digest(true));
    }

    [Fact]
    public async Task ValidLinksCreateProfilesWithoutCreatingUsersMembershipsOrRoles()
    {
        var membership = Id("admin2-membership");
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@id,'Teacher'),(@id,'Student')", ("@id", membership));
        var token = await Access();
        var createdIds = new Dictionary<string, Guid>();
        foreach (var family in new[] { "student", "teacher" })
        {
            var response = await Request(Route(family) + "?membershipId=" + membership + (family == "student" ? "&classId=" + Id("class") : ""), token, Input(family), "POST");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            createdIds[family] = await response.Content.ReadFromJsonAsync<Guid>();
        }
        var login = await Request("/api/Auth/identity/login", body: new LoginDto { Email = _admin2.ToString("N") + "@mt3c.example.test", Password = Password }, method: "POST");
        var ticket = (await login.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
        var selected = await Request("/api/Auth/select-institution", ticket, new SelectInstitutionDto { InstitutionId = _a, MembershipId = membership }, "POST");
        var multi = (await selected.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        foreach (var family in createdIds.Keys)
        {
            var response = await Request(Route(family) + "/me", multi); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(createdIds[family], RowId(await response.Content.ReadFromJsonAsync<JsonObject>()));
        }
        await Db(async db =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _admin2); Assert.Null(user.StudentId); Assert.Null(user.TeacherId);
            Assert.Equal(3, await db.InstitutionMembershipRoles.CountAsync(r => r.MembershipId == membership));
            Assert.Equal(1, await db.InstitutionMemberships.CountAsync(m => m.UserId == _admin2));
        });
    }

    [Theory]
    [InlineData("student")]
    [InlineData("teacher")]
    [InlineData("subject")]
    public async Task InvalidBodiesAreBadRequestsWithoutWrites(string family)
    {
        var token = await Access(); var before = await Digest(false);
        foreach (var body in new[] { Input(family, ""), Input(family, new string('x', 160)) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Request(Route(family), token, body, "POST")).StatusCode);
        Assert.Equal(before, await Digest(false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GateMetadataCannotBypassMissingContextOrWrongPolicy(bool wrongPolicy)
    {
        using var scope = _host.Host.Services.CreateScope();
        var gateType = typeof(InstitutionStudentController).Assembly.GetType("ElectronicRegisterAPI.Api.Security.LegacyEndpointGate")!;
        var gate = (Microsoft.AspNetCore.Mvc.Filters.IAsyncAuthorizationFilter)scope.ServiceProvider.GetRequiredService(gateType);
        var marker = new InstitutionSchoolEndpointAttribute();
        if (wrongPolicy) marker.Policy = InstitutionTokenContract.AccessScheme;
        var action = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor { EndpointMetadata = new List<object> { marker } };
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("sub", _adminUser.ToString()) }, "test"));
        var context = new Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext(new Microsoft.AspNetCore.Mvc.ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), action), new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>());
        await gate.OnAuthorizationAsync(context);
        Assert.Equal(403, Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(context.Result).StatusCode);
        Assert.Throws<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Value);
    }

    [Fact]
    public async Task ConcurrentHttpRequestsOfSharedIdentityKeepSeparateTenantScopes()
    {
        var a = await Access(); var b = await Access(b: true);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            var isB = i % 2 == 0; var family = new[] { "student", "teacher", "subject" }[i % 3];
            var rows = await ArrayResponse(Route(family), isB ? b : a);
            Assert.Equal(new[] { Id(family, isB), Id(family + "-other", isB) }.Order(), rows.Select(RowId).Order());
        }));
    }

    public async Task DisposeAsync()
    {
        if (_host is not null) await _host.DisposeAsync();
        await Db(async db=>
        {
            await using var purge = await MembershipFixturePurge.AllowAsync(db, new[]{_a,_b});
            foreach(var table in new[]{"institution_student_profile","institution_teacher_profile","grade","class_subject","student","teacher","subject","class","biennium_study_path","biennium_study_area","study_path","study_area","biennium"})
            {
                // Closed identifier allowlist; tenant GUIDs remain SQL parameters.
                var sql="DELETE FROM `"+table+"` WHERE institution_id IN ({0},{1})";
                await db.Database.ExecuteSqlRawAsync(sql,_a,_b);
            }
            var users=new[]{_teacherUser,_studentUser,_adminUser,_admin2};
            var memberships=await db.InstitutionMemberships.Where(m=>users.Contains(m.UserId)).Select(m=>m.Id).ToListAsync();
            await db.InstitutionMembershipRoles.Where(r=>memberships.Contains(r.MembershipId)).ExecuteDeleteAsync();
            await db.InstitutionMemberships.Where(m=>users.Contains(m.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(u=>users.Contains(u.Id)).ExecuteDeleteAsync();
            await db.Institutions.Where(i=>i.Id==_a||i.Id==_b).ExecuteDeleteAsync();
        });

    }
}
