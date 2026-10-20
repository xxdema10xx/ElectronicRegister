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
public sealed partial class Mt4bHttpTests(IntegrationFixture fixture) : IAsyncLifetime
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
            foreach(var institution in new[]{_a,_b})db.Institutions.Add(new(){Id=institution,Code="MT4B_"+institution.ToString("N"),Name="Temporary ITS",Status="active"});
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
            foreach(var isB in new[]{false,true})
            {
                var owner=isB?_b:_a;
                foreach(var suffix in new[]{"","-other"}) db.Campuses.Add(new E.Campus { Id=Id("campus"+suffix,isB),InstitutionId=owner,Name="Campus"+suffix,IsActive=true });
                var path=new E.StudyPath { Id=Id("different-path",isB),Name="Different path" }; db.Add(path); db.Entry(path).Property<Guid>("InstitutionId").CurrentValue=owner;
                var bsp=new E.BienniumStudyPath { Id=Id("different-bsp",isB),BienniumStudyAreaId=Id("bsa",isB),StudyPathId=path.Id };db.Add(bsp);db.Entry(bsp).Property<Guid>("InstitutionId").CurrentValue=owner;
                var group=new E.Class { Id=Id("different-class",isB),Name="Different path class",BienniumStudyPathId=bsp.Id };db.Add(group);db.Entry(group).Property<Guid>("InstitutionId").CurrentValue=owner;
            }
            db.Users.AddRange(new[]{_teacherUser,_studentUser,_adminUser,_admin2}.Select(id=>new E.User{Id=id,Email=id.ToString("N")+"@mt4b.example.test",PasswordHash=Hash,Role=id==_teacherUser?"admin":"student",TeacherId=id==_teacherUser?Id("teacher"):null}));
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
        _host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,course:true);
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
        var response = await Request("/api/Auth/identity/login", body: new LoginDto { Email = user.ToString("N") + "@mt4b.example.test", Password = Password }, method: "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
    }
    private async Task<string> Access(string role = "admin", bool b = false)
    {
        var response = await Request("/api/Auth/select-institution", await Ticket(role), new SelectInstitutionDto { InstitutionId = b ? _b : _a, MembershipId = Id(role + "-membership", b) }, "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }
    private const string Editions="/api/institution/course-editions", Enrollments="/api/institution/enrollments";
    private static async Task Expect(HttpResponseMessage response,HttpStatusCode code)
    { Assert.True(response.StatusCode==code,$"Expected {code}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); }
    private object EditionInput(bool b=false,string code="LOCAL",Guid? path=null) => new { StudyPathId=path??Id("path",b),Code=code,Name="Edizione",StartDate=(string?)null,EndDate=(string?)null,Status="planned" };
    private object EnrollmentInput(Guid edition,bool b=false,Guid? student=null) => new { StudentId=student??Id("student",b),CourseEditionId=edition,EnrollmentDate="2026-10-19",CompletionDate=(string?)null,Status="enrolled" };
    private async Task<Guid> CreateEdition(bool b=false,string code="LOCAL")
    {
        var token=await Access(b:b);var response=await Request(Editions,token,EditionInput(b,code),"POST");await Expect(response,HttpStatusCode.Created);
        var location=response.Headers.Location!;Assert.StartsWith(Editions+"/",location.IsAbsoluteUri?location.AbsolutePath:location.OriginalString);await Expect(await Request(location.OriginalString,token),HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }
    private async Task<Guid> CreateEnrollment(Guid edition,bool b=false)
    {
        var token=await Access(b:b); var response=await Request(Enrollments,token,EnrollmentInput(edition,b),"POST");await Expect(response,HttpStatusCode.Created);
        var location=response.Headers.Location!;Assert.StartsWith(Enrollments+"/",location.IsAbsoluteUri?location.AbsolutePath:location.OriginalString);await Expect(await Request(location.OriginalString,token),HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }
    private async Task<JsonArray> Rows(string route,string token)
    { var r=await Request(route,token);await Expect(r,HttpStatusCode.OK);return (await r.Content.ReadFromJsonAsync<JsonArray>())!; }
    private async Task<string> Digest()
    {
        await using var c=await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);var result=new List<string>();
        foreach(var table in new[]{"student","institution_membership","institution_student_profile","class_subject","exam","grade","exam_submission","user"})
        {
            var sql=table=="user"?"SELECT * FROM user WHERE id IN (@u,@v,@w,@x) ORDER BY id":"SELECT * FROM `"+table+"` WHERE institution_id IN (@a,@b) ORDER BY 1";
            await using var cmd=new MySqlCommand(sql,c);cmd.Parameters.AddWithValue("@a",_a);cmd.Parameters.AddWithValue("@b",_b);
            cmd.Parameters.AddWithValue("@u",_teacherUser);cmd.Parameters.AddWithValue("@v",_studentUser);cmd.Parameters.AddWithValue("@w",_adminUser);cmd.Parameters.AddWithValue("@x",_admin2);
            await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync()){var values=new object[reader.FieldCount];reader.GetValues(values);result.Add(System.Text.Json.JsonSerializer.Serialize(values));}
        }
        return string.Join("\n",result);
    }
    [Fact]
    public async Task CatalogCreationFiltersOwnershipAndDescriptiveUpdatesPreserveStatusAndPath()
    {
        var before=await Digest();var a=await CreateEdition();var b=await CreateEdition(true);var admin=await Access();
        using(var request=new HttpRequestMessage(HttpMethod.Get,Editions+"?institutionId="+_b))
        {
            request.Headers.Authorization=new("Bearer",admin);request.Headers.Add("InstitutionId",_b.ToString());
            var result=await _host.Client.SendAsync(request);await Expect(result,HttpStatusCode.OK);
            Assert.Equal(_a,Assert.Single((await result.Content.ReadFromJsonAsync<List<CourseEditionDto>>())!).InstitutionId);
        }
        var dto=(await (await Request(Editions+"/"+a,admin)).Content.ReadFromJsonAsync<CourseEditionDto>())!;
        Assert.Equal(_a,dto.InstitutionId);Assert.Equal(Id("path"),dto.StudyPathId);Assert.Equal("planned",dto.Status);Assert.Equal(64,dto.Version.Length);
        await Expect(await Request(Editions+"/by-code/LOCAL",admin),HttpStatusCode.OK);
        await Expect(await Request(Editions,admin,EditionInput(),"POST"),HttpStatusCode.Conflict);
        await Expect(await Request(Editions,admin,EditionInput(path:Id("path",true),code:"BAD"),"POST"),HttpStatusCode.NotFound);
        var foreign=await Request(Editions+"/"+b,admin);var absent=await Request(Editions+"/"+Guid.NewGuid(),admin);
        await Expect(foreign,HttpStatusCode.NotFound);await Expect(absent,HttpStatusCode.NotFound);Assert.Equal(await absent.Content.ReadAsStringAsync(),await foreign.Content.ReadAsStringAsync());
        var update=new UpdateCourseEditionDto("LOCAL","Revised",new DateOnly(2026,9,1),null,dto.Version);
        await Expect(await Request(Editions+"/"+b,admin,update,"PUT"),HttpStatusCode.NotFound);
        await Expect(await Request(Editions+"/"+a,admin,update,"PUT"),HttpStatusCode.NoContent);
        await Expect(await Request(Editions+"/"+a,admin,update,"PUT"),HttpStatusCode.Conflict);
        Assert.Single(await Rows(Editions+"?studyPathId="+Id("path")+"&pageSize=1",admin));
        Assert.Empty(await Rows(Editions+"?studyPathId="+Id("path",true),admin));Assert.Empty(await Rows(Editions+"?pageNumber=2",admin));
        await Expect(await Request(Editions+"?pageSize=101",admin),HttpStatusCode.BadRequest);
        await Db(async db=>{var row=await db.CourseEditions.AsNoTracking().SingleAsync(e=>e.Id==a);Assert.Equal("Revised",row.Name);Assert.Equal("planned",row.Status);Assert.Equal(Id("path"),row.StudyPathId);Assert.Equal(_a,row.InstitutionId);});
        Assert.Equal(before,await Digest());
    }
    [Fact]
    public async Task CampusLinksAreManyToManyScopedUniqueAndPreservedWhenInactive()
    {
        var a=await CreateEdition();var second=await CreateEdition(code:"SECOND");var admin=await Access();
        Assert.Equal(new[]{Id("campus"),Id("campus-other")}.Order(),(await Rows("/api/institution/campuses",admin)).Select(r=>r!["campusId"]!.GetValue<Guid>()).Order());
        foreach(var campus in new[]{Id("campus"),Id("campus-other")})await Expect(await Request(Editions+"/"+a+"/campuses",admin,new { CampusId=campus },"POST"),HttpStatusCode.Created);
        await Expect(await Request(Editions+"/"+second+"/campuses",admin,new { CampusId=Id("campus") },"POST"),HttpStatusCode.Created);
        await Expect(await Request(Editions+"/"+a+"/campuses",admin,new { CampusId=Id("campus",true) },"POST"),HttpStatusCode.NotFound);
        await Expect(await Request(Editions+"/"+a+"/campuses",admin,new { CampusId=Id("campus") },"POST"),HttpStatusCode.Conflict);
        await Db(async db=>await db.Campuses.Where(c=>c.Id==Id("campus") && c.InstitutionId==_a).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.IsActive,false)));
        Assert.Equal(2,(await Rows(Editions+"/"+a+"/campuses",admin)).Count);
        await Db(async db=>{Assert.Equal(3,await db.CourseEditionCampuses.CountAsync(e=>e.InstitutionId==_a));Assert.Equal(0,await db.CourseEditionCampuses.CountAsync(e=>e.InstitutionId==_b));});
    }
    [Fact]
    public async Task ExplicitClassAssignmentIsPathScopedImmutableAndNeverChangesStudentClass()
    {
        var before=await Digest();var a=await CreateEdition();var second=await CreateEdition(code:"SECOND");var admin=await Access();
        await Expect(await Request(Editions+"/"+a+"/classes/"+Id("class",true),admin,method:"POST"),HttpStatusCode.NotFound);
        await Expect(await Request(Editions+"/"+a+"/classes/"+Id("different-class"),admin,method:"POST"),HttpStatusCode.NotFound);
        await Expect(await Request(Editions+"/"+a+"/classes/"+Id("class"),admin,method:"POST"),HttpStatusCode.NoContent);
        await Expect(await Request(Editions+"/"+second+"/classes/"+Id("class"),admin,method:"POST"),HttpStatusCode.Conflict);
        Assert.Single(await Rows(Editions+"/"+a+"/classes",admin));Assert.Equal(before,await Digest());
        await Db(async db=>Assert.Null(await db.Classes.Where(c=>c.Id==Id("class-other") && EF.Property<Guid>(c,"InstitutionId")==_a).Select(c=>EF.Property<Guid?>(c,"CourseEditionId")).SingleAsync()));
    }
    [Fact]
    public async Task EnrollmentHistoryAndPersonalMembershipProfilesStaySeparateAcrossSharedUser()
    {
        var before=await Digest();var a=await CreateEdition();var b=await CreateEdition(true);var second=await CreateEdition(code:"SECOND");
        var admin=await Access();var enrolled=await CreateEnrollment(a);await CreateEnrollment(b,true);await CreateEnrollment(second);
        await Expect(await Request(Enrollments,admin,EnrollmentInput(a),"POST"),HttpStatusCode.Conflict);
        await Expect(await Request(Enrollments,admin,EnrollmentInput(a,student:Id("student",true)),"POST"),HttpStatusCode.NotFound);
        await Expect(await Request(Enrollments,admin,EnrollmentInput(b),"POST"),HttpStatusCode.NotFound);
        Assert.Equal(2,(await Rows(Enrollments+"?studentId="+Id("student"),admin)).Count);
        Assert.Single(await Rows(Editions+"/"+a+"/enrollments",admin));
        await Expect(await Request(Enrollments+"?studentId="+Id("student",true),admin),HttpStatusCode.NotFound);
        await Expect(await Request(Enrollments+"/"+enrolled,await Access(b:true)),HttpStatusCode.NotFound);
        foreach(var isB in new[]{false,true})
        {
            var own=await Rows(Enrollments+"/me?studentId="+Id("student",!isB),await Access("student",isB));
            Assert.Equal(isB?1:2,own.Count);Assert.All(own,r=>{Assert.Null(r!["studentId"]);Assert.NotEqual(isB?a:b,r["courseEditionId"]!.GetValue<Guid>());});
        }
        await Expect(await Request(Enrollments+"/"+enrolled,await Access("student")),HttpStatusCode.Forbidden);
        Assert.Equal(before,await Digest());
        await Db(async db=>{Assert.Equal(2,await db.StudentEnrollments.CountAsync(e=>e.InstitutionId==_a));Assert.Equal(1,await db.StudentEnrollments.CountAsync(e=>e.InstitutionId==_b));});
    }
    [Theory]
    [InlineData("teacher")]
    [InlineData("student")]
    public async Task NonAdminsCannotUseAnyAdministrativeAction(string role)
    {
        var edition=await CreateEdition();var token=await Access(role);var before=await Digest();
        foreach(var (method,path,body) in AdminActions(edition))await Expect(await Request(path,token,body,method),HttpStatusCode.Forbidden);
        Assert.Equal(before,await Digest());
    }
    private IEnumerable<(string Method,string Path,object? Body)> AdminActions(Guid edition)
    {
        yield return ("GET",Editions,null);yield return ("GET",Editions+"/"+edition,null);yield return ("GET",Editions+"/by-code/LOCAL",null);
        yield return ("POST",Editions,EditionInput());yield return ("PUT",Editions+"/"+edition,new UpdateCourseEditionDto("X","X",null,null,new string('A',64)));
        yield return ("GET","/api/institution/campuses",null);yield return ("GET",Editions+"/"+edition+"/campuses",null);
        yield return ("POST",Editions+"/"+edition+"/campuses",new {CampusId=Id("campus")});yield return ("GET",Editions+"/"+edition+"/classes",null);
        yield return ("POST",Editions+"/"+edition+"/classes/"+Id("class"),null);yield return ("GET",Editions+"/"+edition+"/enrollments",null);
        yield return ("GET",Enrollments+"/"+Guid.NewGuid(),null);yield return ("GET",Enrollments+"?studentId="+Id("student"),null);
        yield return ("POST",Enrollments,EnrollmentInput(edition));
    }
    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public async Task BothServerFlagsRequiredOnEveryAction(bool school,bool course)
    {
        var a=await CreateEdition();await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:school,course:course);
        var token=await Access();foreach(var (method,path,body) in AdminActions(a))await Expect(await Request(path,token,body,method,host),HttpStatusCode.Forbidden);
        await Expect(await Request(Enrollments+"/me",await Access("student"),host:host),HttpStatusCode.Forbidden);
    }
    [Theory]
    [InlineData("membership")]
    [InlineData("institution")]
    [InlineData("roles")]
    public async Task PreviouslyIssuedJwtLosesRightsOnNextOperation(string change)
    {
        var a=await CreateEdition();var token=await Access();
        if(change=="membership")await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id",("@id",Id("admin-membership")));
        else if(change=="institution")await Execute("UPDATE institution SET status='suspended' WHERE id=@id",("@id",_a));
        else await Execute("DELETE FROM institution_membership_role WHERE membership_id=@id",("@id",Id("admin-membership")));
        foreach(var (method,path,body) in AdminActions(a))await Expect(await Request(path,token,body,method),HttpStatusCode.Forbidden);
        await Db(async db=>Assert.Equal(1,await db.CourseEditions.CountAsync(e=>e.InstitutionId==_a)));
    }
    [Fact]
    public async Task MissingSelectedStudentProfileNeverFallsBackToOtherInstitution()
    {
        var a=await CreateEdition();var b=await CreateEdition(true);await CreateEnrollment(a);await CreateEnrollment(b,true);var token=await Access("student");
        await Execute("DELETE FROM institution_student_profile WHERE membership_id=@id",("@id",Id("student-membership")));
        await Expect(await Request(Enrollments+"/me",token),HttpStatusCode.Forbidden);
        Assert.Single(await Rows(Enrollments+"/me",await Access("student",true)));
    }
    [Fact]
    public async Task SelectionLegacyAndAnonymousTokensCannotUseNewRoutesOrOpenGlobalUserApi()
    {
        var a=await CreateEdition();var ticket=await Ticket();
        foreach(var (method,path,body) in AdminActions(a))
        {
            await Expect(await Request(path,body:body,method:method),HttpStatusCode.Unauthorized);
            await Expect(await Request(path,ticket,body,method),HttpStatusCode.Unauthorized);
        }
        await Expect(await Request("/api/Users",await Access()),HttpStatusCode.Unauthorized);
        var legacy=new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer:"mt2-tests",audience:"mt2-client",claims:new[]{new System.Security.Claims.Claim("sub",_adminUser.ToString()),new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role,"admin")},
            expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Mt3cHosting.Key)),"HS256")));
        await Expect(await Request(Editions,legacy),HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task ImmutableFieldsInvalidDatesAndUnapprovedMutationsAreRejected()
    {
        var a=await CreateEdition();var admin=await Access();
        foreach(var body in new object[]{new {StudyPathId=Id("path"),Code="BAD",Name="Bad",Status="planned",InstitutionId=_b},
            new {StudyPathId=Id("path"),Code="BAD",Name="Bad",Status="invented"},
            new {StudyPathId=Id("path"),Code="BAD",Name="Bad",Status="active",StartDate="2026-10-19",EndDate="2025-01-01"}})
            await Expect(await Request(Editions,admin,body,"POST"),HttpStatusCode.BadRequest);
        await Expect(await Request(Enrollments,admin,new {StudentId=Id("student"),CourseEditionId=a,EnrollmentDate="2026-10-19",CompletionDate="2026-10-18",Status="enrolled"},"POST"),HttpStatusCode.BadRequest);
        await Expect(await Request(Editions+"/"+a,admin,new {Code="X",Name="X",Version=new string('A',64),Status="completed"},"PUT"),HttpStatusCode.BadRequest);
        var id=await CreateEnrollment(a);
        foreach(var (path,method) in new[]{(Enrollments+"/"+id,"DELETE"),(Enrollments+"/"+id,"PUT"),(Editions+"/"+a,"DELETE"),(Editions+"/"+a+"/complete","POST"),(Enrollments+"/"+id+"/complete","POST"),(Editions+"/"+a+"/campuses/"+Id("campus"),"DELETE")})
        {var response=await Request(path,admin,method:method);Assert.True(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);}
        await Db(async db=>{Assert.Equal("enrolled",(await db.StudentEnrollments.AsNoTracking().SingleAsync(e=>e.Id==id)).Status);Assert.Equal("planned",(await db.CourseEditions.AsNoTracking().SingleAsync(e=>e.Id==a)).Status);});
    }
    [Fact]
    public async Task ConcurrentUniquenessClassAssignmentAndDescriptionUpdatesHaveSingleWinners()
    {
        var token=await Access();
        var login=await Request("/api/Auth/identity/login",body:new LoginDto {Email=_admin2.ToString("N")+"@mt4b.example.test",Password=Password},method:"POST");
        var ticket=(await login.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
        var selection=await Request("/api/Auth/select-institution",ticket,new SelectInstitutionDto {InstitutionId=_a,MembershipId=Id("admin2-membership")},"POST");
        var secondToken=(await selection.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
        async Task Race(Func<string,Task<HttpResponseMessage>> call,HttpStatusCode success)
        {var responses=await Task.WhenAll(call(token),call(secondToken));Assert.Equal(new[]{success,HttpStatusCode.Conflict}.Order(),responses.Select(r=>r.StatusCode).Order());}
        await Race(t=>Request(Editions,t,EditionInput(),"POST"),HttpStatusCode.Created);
        Guid a=Guid.Empty;await Db(async db=>a=await db.CourseEditions.Where(e=>e.InstitutionId==_a).Select(e=>e.Id).SingleAsync());
        await Race(t=>Request(Enrollments,t,EnrollmentInput(a),"POST"),HttpStatusCode.Created);
        await Race(t=>Request(Editions+"/"+a+"/campuses",t,new {CampusId=Id("campus")},"POST"),HttpStatusCode.Created);
        var b=await CreateEdition(code:"SECOND");
        var classes=await Task.WhenAll(Request(Editions+"/"+a+"/classes/"+Id("class"),token,method:"POST"),Request(Editions+"/"+b+"/classes/"+Id("class"),secondToken,method:"POST"));
        Assert.Equal(new[]{HttpStatusCode.NoContent,HttpStatusCode.Conflict}.Order(),classes.Select(x=>x.StatusCode).Order());
        var row=(await (await Request(Editions+"/"+a,token)).Content.ReadFromJsonAsync<CourseEditionDto>())!;
        await Race(t=>Request(Editions+"/"+a,t,new UpdateCourseEditionDto("LOCAL",t==token?"First":"Second",null,null,row.Version),"PUT"),HttpStatusCode.NoContent);
        await Db(async db=>{Assert.Equal(1,await db.StudentEnrollments.CountAsync(e=>e.InstitutionId==_a));Assert.Equal(1,await db.CourseEditionCampuses.CountAsync(e=>e.InstitutionId==_a));var group=await db.Classes.SingleAsync(c=>c.Id==Id("class"));Assert.Contains(db.Entry(group).Property<Guid?>("CourseEditionId").CurrentValue,new Guid?[]{a,b});});
    }
    [Fact]
    public async Task ParallelTenantQueriesAndPostsNeverMixDataAndSqlWritersCarryOwner()
    {
        using var observer=new SqlObserver();using var subscription=DiagnosticListener.AllListeners.Subscribe(observer);
        var a=await CreateEdition();var b=await CreateEdition(true);var adminA=await Access();var adminB=await Access(b:true);
        await Task.WhenAll(CreateEnrollment(a),CreateEnrollment(b,true));
        await Task.WhenAll(Enumerable.Range(0,8).Select(async i=>{var isB=i%2==0;var rows=await Rows(Editions,isB?adminB:adminA);Assert.Single(rows);Assert.Equal(isB?_b:_a,rows[0]!["institutionId"]!.GetValue<Guid>());}));
        await Expect(await Request(Editions+"/"+a+"/campuses",adminA,new {CampusId=Id("campus")},"POST"),HttpStatusCode.Created);
        await Expect(await Request(Editions+"/"+a+"/classes/"+Id("class"),adminA,method:"POST"),HttpStatusCode.NoContent);
        await Rows(Enrollments+"/me",await Access("student"));await Rows(Editions+"/"+a+"/classes",adminA);await Rows(Editions+"/"+a+"/campuses",adminA);
        var dto=(await (await Request(Editions+"/"+a,adminA)).Content.ReadFromJsonAsync<CourseEditionDto>())!;
        await Expect(await Request(Editions+"/"+a,adminA,new UpdateCourseEditionDto("LOCAL","SQL",null,null,dto.Version),"PUT"),HttpStatusCode.NoContent);
        var commands=observer.Commands.Where(c=>new[]{"course_edition","student_enrollment"}.Any(t=>c.Sql.Contains(t,StringComparison.OrdinalIgnoreCase))).ToArray();
        Assert.NotEmpty(commands);Assert.All(commands,c=>Assert.Contains("institution_id",c.Sql,StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands,c=>c.Sql.Contains("INSERT INTO `course_edition`") && c.Guids.Contains(_a));
        Assert.Contains(commands,c=>c.Sql.Contains("INSERT INTO `student_enrollment`") && c.Guids.Contains(_b));
        Assert.Contains(commands,c=>c.Sql.Contains("UPDATE `course_edition`") && c.Sql.Contains("WHERE") && c.Guids.Contains(_a));
        Assert.Contains(commands,c=>c.Sql.Contains("INSERT INTO `course_edition_campus`") && c.Guids.Contains(_a));
        Assert.Contains(observer.Commands,c=>Regex.IsMatch(c.Sql,"UPDATE `class`[\\s\\S]*?WHERE[^;]*institution_id") && c.Guids.Contains(_a));
        Assert.All(commands.Where(c=>c.Sql.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase)),c=>Assert.Matches("(?is)WHERE.*institution_id",c.Sql));
        Assert.DoesNotContain(commands,c=>c.Sql.Contains("WHERE ROW_COUNT()",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commands,c=>c.Sql.Contains("SELECT *",StringComparison.OrdinalIgnoreCase));
        var dir=EvidenceDirectory();await File.WriteAllTextAsync(Path.Combine(dir,"course-sql.json"),System.Text.Json.JsonSerializer.Serialize(commands,new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
    }
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task RealBootstrapInventoriesExactNewPoliciesAndKeepsOldRoutesClosed(string environment)
    {
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,course:true,environment:environment);
        var policy=await host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>().GetPolicyAsync(InstitutionCourseEndpointAttribute.PolicyName);
        Assert.Equal(new[]{InstitutionTokenContract.AccessScheme},policy!.AuthenticationSchemes);
        var all=host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>().ToArray();
        var mvc=all.Where(e=>e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is not null).ToArray();
        Assert.Equal(156,mvc.Length);
        Assert.Equal(2,mvc.Count(e=>e.Metadata.GetMetadata<InstitutionMembershipAdminEndpointAttribute>() is not null));
        var previous=mvc.Where(e=>e.Metadata.GetMetadata<InstitutionMembershipAdminEndpointAttribute>() is null).ToArray();
        Assert.Equal(154,previous.Length);Assert.Equal(135,previous.Count(e=>e.Metadata.GetMetadata<InstitutionCourseEndpointAttribute>() is null));
        var rows=all.SelectMany(e=>(e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods??new[]{"GET"}).Select(method=>new {
            method,route=e.RoutePattern.RawText,course=e.Metadata.GetMetadata<InstitutionCourseEndpointAttribute>() is not null,
            policy=e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Select(a=>a.Policy).Where(p=>!string.IsNullOrEmpty(p)).ToArray(),
            roles=e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Select(a=>a.Roles).Where(p=>!string.IsNullOrEmpty(p)).ToArray(),
            manager=e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()?.ControllerTypeInfo.DeclaredConstructors.SelectMany(c=>c.GetParameters()).Select(p=>p.ParameterType.Name).ToArray(),
            repository=e.Metadata.GetMetadata<InstitutionCourseEndpointAttribute>() is not null?"ICourseStructureRepository":"existing (see MT3E inventory)",
            ownership=e.Metadata.GetMetadata<InstitutionCourseEndpointAttribute>() is not null?"verified TenantContext + SQL owner + composite FK":"existing checkpoint scope/gate",
            rollout=e.Metadata.GetMetadata<InstitutionCourseEndpointAttribute>() is not null?"CourseApi && SchoolApi":e.Metadata.GetMetadata<InstitutionExamEndpointAttribute>() is not null?"ExamApi && SchoolApi":e.Metadata.GetMetadata<InstitutionSchoolEndpointAttribute>() is not null?"SchoolApi":"identity/legacy gate or Development diagnostics",
            positiveTest=e.RoutePattern.RawText!.Contains("/transition")?nameof(Mt4cLifecycle_EnrolledTerminatesWithExplicitDateReasonRevisionAndHistory):e.RoutePattern.RawText.Contains("/completion-date")?nameof(Mt4cLifecycle_CorrectionKeepsThePreviousTerminalDateAndCannotCorrectEnrolled):e.RoutePattern.RawText.Contains("/history")||e.RoutePattern.RawText.Contains("/audit")?nameof(Mt4cLifecycle_AllMt4bMutationsAreAuditedAtomicallyWithoutDuplicateClassEvents):e.RoutePattern.RawText.Contains("enrollments")?nameof(EnrollmentHistoryAndPersonalMembershipProfilesStaySeparateAcrossSharedUser):e.RoutePattern.RawText.Contains("campuses")?nameof(CampusLinksAreManyToManyScopedUniqueAndPreservedWhenInactive):e.RoutePattern.RawText.Contains("classes")?nameof(ExplicitClassAssignmentIsPathScopedImmutableAndNeverChangesStudentClass):nameof(CatalogCreationFiltersOwnershipAndDescriptiveUpdatesPreserveStatusAndPath),
            negativeTest=e.RoutePattern.RawText.Contains("/me")?nameof(MissingSelectedStudentProfileNeverFallsBackToOtherInstitution):nameof(NonAdminsCannotUseAnyAdministrativeAction)
        })).ToArray();
        var added=rows.Where(e=>e.course).ToArray();Assert.Equal(19,added.Length);
        Assert.All(added,e=>{Assert.Equal(new[]{InstitutionCourseEndpointAttribute.PolicyName},e.policy);Assert.Equal(new[]{nameof(IInstitutionCourseManager)},e.manager);Assert.Single(e.roles);Assert.Contains(e.roles[0],new[]{"InstitutionAdmin","Student"});});
        Assert.DoesNotContain(added,e=>e.method=="DELETE");
        await File.WriteAllTextAsync(Path.Combine(EvidenceDirectory(),"routes-"+environment+".json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
        await Expect(await Request(Editions,await Access(),host:host),HttpStatusCode.OK);
        await Expect(await Request("/api/Users",await Access(),host:host),HttpStatusCode.Unauthorized);
        await Expect(await host.Client.GetAsync("/openapi/v1.json"),environment=="Development"?HttpStatusCode.OK:HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task AdminForeignResourceMatrixIsIndistinguishableFromMissingAndLeavesBothGraphsIntact()
    {
        var a=await CreateEdition();var b=await CreateEdition(true);var foreignEnrollment=await CreateEnrollment(b,true);var token=await Access();var before=await Digest();
        foreach(var id in new[]{b,Guid.NewGuid()})
        {
            foreach(var suffix in new[]{"","/campuses","/classes","/enrollments"})await Expect(await Request(Editions+"/"+id+suffix,token),HttpStatusCode.NotFound);
            await Expect(await Request(Editions+"/"+id,token,new UpdateCourseEditionDto("X","X",null,null,new string('A',64)),"PUT"),HttpStatusCode.NotFound);
            await Expect(await Request(Editions+"/"+id+"/campuses",token,new {CampusId=Id("campus")},"POST"),HttpStatusCode.NotFound);
            await Expect(await Request(Editions+"/"+id+"/classes/"+Id("class"),token,method:"POST"),HttpStatusCode.NotFound);
        }
        await Expect(await Request(Enrollments+"/"+foreignEnrollment,token),HttpStatusCode.NotFound);
        Assert.Empty(await Rows(Enrollments+"?studentId="+Id("student-other"),token));Assert.Empty(await Rows(Editions+"/"+a+"/enrollments",token));
        await Expect(await Request(Enrollments,token,new {StudentId=Id("student"),CourseEditionId=a,EnrollmentDate="2026-10-19",Status="enrolled",InstitutionId=_b},"POST"),HttpStatusCode.BadRequest);
        Assert.Equal(before,await Digest());await Db(async db=>Assert.Equal(0,await db.StudentEnrollments.CountAsync(e=>e.InstitutionId==_a)));
    }
    [Theory]
    [InlineData("enrolled")]
    [InlineData("completed")]
    [InlineData("withdrawn")]
    [InlineData("cancelled")]
    public async Task ExplicitTechnicalStatusesRemainReadableForStudentsWithoutAnyAccount(string status)
    {
        var a=await CreateEdition();var token=await Access();var before=await Digest();
        var response=await Request(Enrollments,token,new {StudentId=Id("student-other"),CourseEditionId=a,EnrollmentDate="2026-09-01",CompletionDate="2026-10-19",Status=status},"POST");
        await Expect(response,HttpStatusCode.Created);var id=await response.Content.ReadFromJsonAsync<Guid>();
        var row=(await (await Request(Enrollments+"/"+id,token)).Content.ReadFromJsonAsync<StudentEnrollmentDto>())!;
        Assert.Equal(status,row.Status);Assert.Equal(new DateOnly(2026,9,1),row.EnrollmentDate);Assert.Equal(new DateOnly(2026,10,19),row.CompletionDate);
        Assert.Equal(before,await Digest());Assert.Empty(await Rows(Enrollments+"/me",await Access("student")));
    }
    [Theory]
    [InlineData("planned")]
    [InlineData("active")]
    [InlineData("completed")]
    [InlineData("cancelled")]
    public async Task EditionStatusMustBeExplicitAndIsNeverDerivedFromDates(string status)
    {
        var token=await Access();var response=await Request(Editions,token,new {StudyPathId=Id("path"),Code="STATE",Name="Explicit",Status=status,StartDate="2020-01-01",EndDate="2020-02-01"},"POST");
        await Expect(response,HttpStatusCode.Created);var id=await response.Content.ReadFromJsonAsync<Guid>();
        await Db(async db=>Assert.Equal(status,(await db.CourseEditions.AsNoTracking().SingleAsync(e=>e.Id==id)).Status));
        await Expect(await Request(Editions,token,new {StudyPathId=Id("path"),Code="MISSING",Name="Missing"},"POST"),HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task AbsentOrInvalidCourseFlagCannotBeEnabledByClientAndWorkerFlagDoesNotOpenHttp()
    {
        await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true);
        var config=host.Host.Services.GetRequiredService<IConfiguration>();var token=await Access();
        foreach(var value in new string?[]{null,"invalid","false"})
        {
            config[InstitutionCourseEndpointAttribute.RolloutKey]=value;config["Multitenancy:InstitutionWorker:Enabled"]="true";
            await Expect(await Request(Editions+"?Multitenancy:CourseApi:Enabled=true",token,host:host),HttpStatusCode.Forbidden);
        }
    }
    [Theory]
    [InlineData("identity")]
    [InlineData("school")]
    [InlineData("exam")]
    [InlineData("duplicate")]
    [InlineData("policy")]
    [InlineData("scheme")]
    [InlineData("anonymous")]
    [InlineData("wrong-marker-policy")]
    public async Task GateRejectsCombinedMarkersAndAlternativeAuthorizationEvenWithVerifiedContext(string kind)
    {
        using var scope=_host.Host.Services.CreateScope();
        var current=scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        current.GetType().GetMethod("Set",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
            .Invoke(current,new object[]{new TenantContext(_adminUser,_a,Id("admin-membership"),new[]{InstitutionRole.InstitutionAdmin})});
        var marker=new InstitutionCourseEndpointAttribute();var metadata=new List<object>{marker};
        metadata.Add(kind switch {
            "identity"=>new InstitutionIdentityEndpointAttribute(),"school"=>new InstitutionSchoolEndpointAttribute(),"exam"=>new InstitutionExamEndpointAttribute(),
            "duplicate"=>new InstitutionCourseEndpointAttribute(),"policy"=>new Microsoft.AspNetCore.Authorization.AuthorizeAttribute {Policy=InstitutionTokenContract.AccessScheme},
            "scheme"=>new Microsoft.AspNetCore.Authorization.AuthorizeAttribute {AuthenticationSchemes="Bearer"},"anonymous"=>new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute(),
            _=>new Microsoft.AspNetCore.Authorization.AuthorizeAttribute {Roles="InstitutionAdmin"}});
        if(kind=="wrong-marker-policy")marker.Policy=InstitutionTokenContract.AccessScheme;
        var gateType=typeof(InstitutionCourseEditionController).Assembly.GetType("ElectronicRegisterAPI.Api.Security.LegacyEndpointGate")!;
        var gate=(Microsoft.AspNetCore.Mvc.Filters.IAsyncAuthorizationFilter)scope.ServiceProvider.GetRequiredService(gateType);
        var action=new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor {EndpointMetadata=metadata};
        var http=new Microsoft.AspNetCore.Http.DefaultHttpContext {RequestServices=scope.ServiceProvider,User=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim("sub",_adminUser.ToString())},"test"))};
        var context=new Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext(new Microsoft.AspNetCore.Mvc.ActionContext(http,new Microsoft.AspNetCore.Routing.RouteData(),action),new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>());
        await gate.OnAuthorizationAsync(context);Assert.Equal(403,Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(context.Result).StatusCode);
    }
    private static string EvidenceDirectory()
    {var p=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tests/Integration/TestResults/mt4b"));Directory.CreateDirectory(p);return p;}
    private sealed record CapturedCommand(string Sql, Guid[] Guids);
    private sealed class SqlObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        public ConcurrentQueue<CapturedCommand> Commands { get; } = new();
        public void OnNext(DiagnosticListener listener) { if (listener.Name == "Microsoft.EntityFrameworkCore") _subscriptions.Add(listener.Subscribe(this)); }
        public void OnNext(KeyValuePair<string, object?> item)
        {
            if (item.Value is CommandExecutedEventData data && !data.Command.CommandText.Contains("JOIN mt3a_legacy_scope"))
                Commands.Enqueue(new(data.Command.CommandText, data.Command.Parameters.Cast<DbParameter>().Select(p => Guid.TryParse(p.Value?.ToString(), out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToArray()));
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
        public void Dispose() { foreach (var subscription in _subscriptions) subscription.Dispose(); }
    }

    public async Task DisposeAsync()
    {
        if (_host is not null) await _host.DisposeAsync();
        await Db(async db=>
        {
            await using var purge = await MembershipFixturePurge.AllowAsync(db, new[]{_a,_b});
            // Privileged fixture maintenance, never an HTTP operation; scope cannot address holding/real tenants.
            await db.Database.OpenConnectionAsync();
            foreach(var owner in new[]{_a,_b})
            {
                try {
                    await db.Database.ExecuteSqlInterpolatedAsync($"SET @er_mt4c_purge_owner={owner}");
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM course_administration_event WHERE institution_id={owner}");
                } finally { await db.Database.ExecuteSqlRawAsync("SET @er_mt4c_purge_owner=NULL"); }
            }
            await db.Database.CloseConnectionAsync();
            foreach(var table in new[]{"student_enrollment","course_edition_campus","institution_student_profile","institution_teacher_profile","grade","class_subject","student","teacher","subject","class","course_edition","campus","biennium_study_path","biennium_study_area","study_path","study_area","biennium"})
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
