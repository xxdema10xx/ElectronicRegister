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

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mt3bSchoolTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private readonly Guid _a=Guid.NewGuid(),_b=Guid.NewGuid(),_teacherUser=Guid.NewGuid(),_studentUser=Guid.NewGuid(),_adminUser=Guid.NewGuid(),_admin2=Guid.NewGuid();
    private readonly Dictionary<string,Guid[]> _ids=new();
    private readonly Capture _sql=new();
    private ServiceProvider _provider=null!;
    private Guid Id(string kind,bool b=false) { if(!_ids.TryGetValue(kind,out var ids))_ids[kind]=ids=new[]{Guid.NewGuid(),Guid.NewGuid()};return ids[b?1:0]; }
    private TenantContext Context(string role="admin",bool b=false) => new(role=="teacher"?_teacherUser:role=="student"?_studentUser:_adminUser,b?_b:_a,Id(role+"-membership",b),new[]{role=="teacher"?InstitutionRole.Teacher:role=="student"?InstitutionRole.Student:InstitutionRole.InstitutionAdmin});
    private async Task<T> Call<TService,T>(TenantContext? tenant,Func<TService,Task<T>> action) where TService:notnull
    {
        using var scope=_provider.CreateScope();
        if(tenant!=null)
        {
            // Token issuance/setup precedes the measured service operation.
            using var identityScope=fixture.ScopeFactory.CreateScope();
            var verified=await PersistedTestTenant.ReadAsync(identityScope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>(),tenant);
            scope.ServiceProvider.GetRequiredService<TestCurrent>().Set(verified);
        }
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
    private async Task Call<TService>(TenantContext? t,Func<TService,Task> action) where TService:notnull
    { await Call<TService,bool>(t,async service=>{await action(service);return true;}); }
    private async Task Db(Func<ElectronicRegisterContext,Task> action)
    { using var scope=fixture.ScopeFactory.CreateScope();await action(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>()); }
    private async Task<object?> Scalar(string sql,params (string,object)[] values)
    { await using var c=await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);await using var cmd=new MySqlCommand(sql,c);foreach(var (key,value) in values)cmd.Parameters.AddWithValue(key,value);return await cmd.ExecuteScalarAsync(); }
    private async Task Execute(string sql,params (string,object)[] values)
    { await using var c=await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);await using var cmd=new MySqlCommand(sql,c);foreach(var (key,value) in values)cmd.Parameters.AddWithValue(key,value);await cmd.ExecuteNonQueryAsync(); }

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["ConnectionStrings:DefaultConnection"]=fixture.ConnectionString }).Build();
        var services=new ServiceCollection();services.AddInfrastructure(config);services.AddBusinessServices();services.AddApplicationManagers();
        services.AddScoped<TestCurrent>();services.AddScoped<ICurrentTenant>(sp=>sp.GetRequiredService<TestCurrent>());
        services.AddDbContext<ElectronicRegisterContext>(options=>options.AddInterceptors(_sql));
        _provider=services.BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true,ValidateOnBuild=true});
        await Db(async db=>
        {
            foreach(var institution in new[]{_a,_b})db.Institutions.Add(new(){Id=institution,Code="MT3B_"+institution.ToString("N"),Name="Temporary ITS",Status="active"});
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
            db.Users.AddRange(new[]{_teacherUser,_studentUser,_adminUser,_admin2}.Select(id=>new E.User{Id=id,Email=id.ToString("N")+"@mt3b.example.test",PasswordHash="fixture-only",Role=id==_teacherUser?"admin":"student",TeacherId=id==_teacherUser?Id("teacher"):null}));
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
    }

    [Fact]
    public async Task StudentDirectoryFiltersAllLookupsAndSqlCountForAdminAndAssignedTeacher()
    {
        foreach(var role in new[]{"admin","teacher"})
        {
            var t=Context(role);var expected=role=="admin"?2:1;
            var all=await Call<IInstitutionStudentManager,List<StudentDto>>(t,m=>m.GetAllAsync());Assert.Equal(expected,all.Count);Assert.All(all,s=>Assert.Contains(s.Id,new[]{Id("student"),Id("student-other")}));
            Assert.Equal(expected,await Call<IInstitutionStudentManager,int>(t,m=>m.CountAsync()));
            Assert.Equal(expected,(await Call<IInstitutionStudentManager,List<StudentDto>>(t,m=>m.GetByLastNameAsync("Same"))).Count);
            Assert.Equal(expected,(await Call<IInstitutionStudentManager,List<StudentDto>>(t,m=>m.GetByIdsAsync(new[]{Id("student"),Id("student-other"),Id("student",true)}))).Count);
            Assert.Null(await Call<IInstitutionStudentManager,StudentDto?>(t,m=>m.GetByIdAsync(Id("student",true))));
        }
        Assert.Null(await Call<IInstitutionStudentManager,StudentDto?>(Context("teacher"),m=>m.GetByIdAsync(Id("student-other"))));
    }

    [Fact]
    public async Task TeacherWithNoAssignmentsSeesNoStudentOrSubjectAndNeverFallsBackToGlobalRole()
    {
        await Execute("DELETE FROM class_subject WHERE teacher_id=@id",("@id",Id("teacher")));
        Assert.Empty(await Call<IInstitutionStudentManager,List<StudentDto>>(Context("teacher"),m=>m.GetAllAsync()));
        Assert.Equal(0,await Call<IInstitutionStudentManager,int>(Context("teacher"),m=>m.CountAsync()));
        Assert.Empty(await Call<IInstitutionSubjectManager,List<SubjectDto>>(Context("teacher"),m=>m.GetAllAsync()));
    }

    [Fact]
    public async Task StudentOwnProfileIsDistinctFromDirectoryAndSubjectByTeacherKeepsClassIntersection()
    {
        var t=Context("student");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Call<IInstitutionStudentManager,List<StudentDto>>(t,m=>m.GetAllAsync()));
        Assert.Equal(Id("student"),(await Call<IInstitutionStudentManager,StudentDto?>(t,m=>m.GetOwnProfileAsync()))!.Id);
        Assert.Equal(new[]{Id("subject")},(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetAllAsync())).Select(s=>s.Id));
        Assert.Empty(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetByTeacherAsync(Id("teacher-other"))));
        Assert.Empty(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetByTeacherAsync(Id("teacher",true))));
        Assert.Equal(new[]{Id("subject")},(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetByTeacherAsync(Id("teacher")))).Select(s=>s.Id));
        await Execute("UPDATE student SET class_id=NULL WHERE id=@id",("@id",Id("student")));
        Assert.Empty(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetAllAsync()));
        Assert.Empty(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetByTeacherAsync(Id("teacher"))));
    }

    [Fact]
    public async Task SharedGlobalUserResolvesDifferentTeacherProfilesAndDirectoryStaysInTenant()
    {
        foreach(var b in new[]{false,true})
        {
            var t=Context("teacher",b);
            Assert.Equal(Id("teacher",b),(await Call<IInstitutionTeacherManager,TeacherDto?>(t,m=>m.GetOwnProfileAsync()))!.Id);
            Assert.Equal(2,(await Call<IInstitutionTeacherManager,List<TeacherDto>>(t,m=>m.GetAllAsync())).Count);
            Assert.Equal(2,await Call<IInstitutionTeacherManager,int>(t,m=>m.CountAsync()));
            Assert.Equal(2,(await Call<IInstitutionTeacherManager,List<TeacherDto>>(t,m=>m.GetByLastNameAsync("Same"))).Count);
            Assert.Single(await Call<IInstitutionTeacherManager,List<TeacherDto>>(t,m=>m.GetByIdsAsync(new[]{Id("teacher"),Id("teacher",true)})));
            Assert.Null(await Call<IInstitutionTeacherManager,TeacherDto?>(t,m=>m.GetByIdAsync(Id("teacher",!b))));
            Assert.Single(await Call<IInstitutionTeacherManager,List<TeacherDto>>(t,m=>m.GetForClassAsync(Id("class",b))));
            Assert.Empty(await Call<IInstitutionTeacherManager,List<TeacherDto>>(t,m=>m.GetForClassAsync(Id("class",!b))));
        }
        Assert.Equal(2,(await Call<IInstitutionTeacherManager,List<TeacherDto>>(Context("student"),m=>m.GetAllAsync())).Count);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Call<IInstitutionTeacherManager,int>(Context("student"),m=>m.CountAsync()));
    }

    [Theory][InlineData("admin",2)][InlineData("teacher",1)][InlineData("student",1)]
    public async Task SubjectNamesAreIndependentAndAllLookupsUseVisibility(string role,int expected)
    {
        var t=Context(role);
        Assert.Equal(expected,(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetAllAsync())).Count);
        Assert.Equal(expected,await Call<IInstitutionSubjectManager,int>(t,m=>m.CountAsync()));
        Assert.Equal(Id("subject"),(await Call<IInstitutionSubjectManager,SubjectDto?>(t,m=>m.GetByNameAsync("Matematica")))!.Id);
        Assert.Null(await Call<IInstitutionSubjectManager,SubjectDto?>(t,m=>m.GetByIdAsync(Id("subject",true))));
        Assert.Single(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetByIdsAsync(new[]{Id("subject"),Id("subject",true)})));
        if(role!="admin")Assert.Empty(await Call<IInstitutionSubjectManager,List<SubjectDto>>(t,m=>m.GetByTeacherAsync(Id("teacher-other"))));
        Assert.Equal(Id("subject",true),(await Call<IInstitutionSubjectManager,SubjectDto?>(Context("admin",true),m=>m.GetByNameAsync("Matematica")))!.Id);
    }

    [Theory][InlineData("student")][InlineData("teacher")][InlineData("subject")]
    public async Task ScopedCrudWritesExplicitOwnerAndLeavesBUnchanged(string family)
    {
        var before=await TenantDigest(_b);_sql.Clear();var id=await Add(family,Context());
        var command=Assert.Single(_sql.Commands.Where(c=>c.Sql.Contains("INSERT INTO `"+family+"`")));
        var match=Regex.Match(command.Sql,@"INSERT INTO `\w+` \(([^)]+)\)\s*VALUES \(([^)]+)\)");Assert.True(match.Success,command.Sql);
        var cols=match.Groups[1].Value.Split(',').Select(s=>s.Trim(' ','`')).ToArray();var ps=match.Groups[2].Value.Split(',').Select(s=>s.Trim()).ToArray();
        Assert.Equal(_a,command.Guids[ps[Array.IndexOf(cols,"institution_id")]]);
        Assert.Equal(_a.ToString(),(await Scalar($"SELECT institution_id FROM `{family}` WHERE id=@id",("@id",id)))!.ToString());
        await Update(family,Context(),id);Assert.Equal("Changed",(await Scalar($"SELECT {(family=="subject"?"name":"first_name")} FROM `{family}` WHERE id=@id",("@id",id)))!.ToString());
        await Delete(family,Context(),id);Assert.Equal(0L,Convert.ToInt64(await Scalar($"SELECT COUNT(*) FROM `{family}` WHERE id=@id",("@id",id))));
        Assert.Equal(before,await TenantDigest(_b));
    }

    [Theory][InlineData("student",false)][InlineData("student",true)][InlineData("teacher",false)][InlineData("teacher",true)][InlineData("subject",false)][InlineData("subject",true)]
    public async Task CrossTenantMutationIsGenericNotFoundAndBUnchanged(string family,bool delete)
    {
        var before=await TenantDigest(_b);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>delete?Delete(family,Context(),Id(family,true)):Update(family,Context(),Id(family,true)));
        Assert.Equal(before,await TenantDigest(_b));
    }

    [Theory][InlineData("student")][InlineData("teacher")][InlineData("subject")]
    public async Task TeacherAndStudentCannotUseAdministrativeWriters(string family)
    {
        var before=await TenantDigest(_a);
        foreach(var role in new[]{"teacher","student"}) await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Add(family,Context(role)));
        Assert.Equal(before,await TenantDigest(_a));
    }

    [Theory][InlineData("membership")][InlineData("institution")][InlineData("role-removed")][InlineData("role-added")][InlineData("missing-profile")]
    public async Task NewOperationRechecksRevocationAndNeverFallsBack(string change)
    {
        var t=Context("teacher");Assert.NotNull(await Call<IInstitutionTeacherManager,TeacherDto?>(t,m=>m.GetOwnProfileAsync()));
        if(change=="membership")await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id",("@id",t.MembershipId));
        if(change=="institution")await Execute("UPDATE institution SET status='suspended' WHERE id=@id",("@id",_a));
        if(change=="role-removed")await Execute("DELETE FROM institution_membership_role WHERE membership_id=@id AND role='Teacher'",("@id",t.MembershipId));
        if(change=="role-added")await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@id,'InstitutionAdmin')",("@id",t.MembershipId));
        if(change=="missing-profile")await Execute("DELETE FROM institution_teacher_profile WHERE membership_id=@id",("@id",t.MembershipId));
        var before=await TenantDigest(_a);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Call<IInstitutionStudentManager,List<StudentDto>>(t,m=>m.GetAllAsync()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Add("subject",t));
        Assert.Equal(before,await TenantDigest(_a));
        Assert.Equal(Id("teacher",true),(await Call<IInstitutionTeacherManager,TeacherDto?>(Context("teacher",true),m=>m.GetOwnProfileAsync()))!.Id);
    }

    [Theory][InlineData("none")][InlineData("forged-role")][InlineData("wrong-membership")][InlineData("wrong-tenant")]
    public async Task ContextCannotInventPermissions(string fault)
    {
        TenantContext? t=fault=="none"?null:new(_teacherUser,fault=="wrong-tenant"?_b:_a,fault=="wrong-membership"?Id("admin-membership"):Id("teacher-membership"),new[]{InstitutionRole.InstitutionAdmin});
        var before=await TenantDigest(_a);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Add("student",t));Assert.Equal(before,await TenantDigest(_a));
    }

    [Fact]
    public async Task MultiRoleAdminUsesAdminPermissionButTeacherOperationRequiresItsOwnProfile()
    {
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@m,'Teacher')",("@m",Id("admin-membership")));
        var t=new TenantContext(_adminUser,_a,Id("admin-membership"),new[]{InstitutionRole.InstitutionAdmin,InstitutionRole.Teacher});
        Assert.Equal(2,(await Call<IInstitutionStudentManager,List<StudentDto>>(t,m=>m.GetAllAsync())).Count);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Call<IInstitutionTeacherManager,TeacherDto?>(t,m=>m.GetOwnProfileAsync()));
        var created=await Call<IInstitutionTeacherManager,Guid>(t,m=>m.AddAsync(new(){FirstName="Admin",LastName="Teacher"},t.MembershipId));
        Assert.Equal(created,(await Call<IInstitutionTeacherManager,TeacherDto?>(t,m=>m.GetOwnProfileAsync()))!.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Call<IInstitutionStudentManager,StudentDto?>(t,m=>m.GetOwnProfileAsync()));
    }

    [Fact]
    public async Task InvalidClassOrMembershipRejectsAndDuplicateLinkRollsBackNewProfile()
    {
        var before=await TenantDigest(_a);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Call<IInstitutionStudentManager,Guid>(Context(),m=>m.AddAsync(new(){FirstName="Wrong",LastName="Class"},Id("class",true))));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Call<IInstitutionTeacherManager,Guid>(Context(),m=>m.AddAsync(new(){FirstName="Wrong",LastName="Membership"},Id("teacher-membership",true))));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Call<IInstitutionTeacherManager,Guid>(Context(),m=>m.AddAsync(new(){FirstName="Missing",LastName="Role"},Id("admin-membership"))));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>Call<IInstitutionTeacherManager,Guid>(Context(),m=>m.AddAsync(new(){FirstName="Duplicate",LastName="Profile"},Id("teacher-membership"))));
        Assert.Equal(before,await TenantDigest(_a));
    }

    [Fact]
    public async Task StudentCreationLinksOnlyExistingAuthorizedMembershipWithoutDuplicatingGlobalIdentity()
    {
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@m,'Student')",("@m",Id("admin2-membership")));
        var id=await Call<IInstitutionStudentManager,Guid>(Context(),m=>m.AddAsync(new(){FirstName="Linked",LastName="Student"},membershipId:Id("admin2-membership")));
        var t=new TenantContext(_admin2,_a,Id("admin2-membership"),new[]{InstitutionRole.InstitutionAdmin,InstitutionRole.Student});
        Assert.Equal(id,(await Call<IInstitutionStudentManager,StudentDto?>(t,m=>m.GetOwnProfileAsync()))!.Id);
        Assert.Equal(1L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM user WHERE id=@u",("@u",_admin2))));
        Assert.Equal(DBNull.Value,await Scalar("SELECT student_id FROM user WHERE id=@u",("@u",_admin2)));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task SuspendedAdminCannotWriteAnyFamilyAndBRemainsAvailable(bool institution)
    {
        if(institution)await Execute("UPDATE institution SET status='suspended' WHERE id=@id",("@id",_a));
        else await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id",("@id",Id("admin-membership")));
        var before=await TenantDigest(_a);
        foreach(var family in new[]{"student","teacher","subject"})await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Add(family,Context()));
        Assert.Equal(before,await TenantDigest(_a));
        Assert.Equal(2,await Call<IInstitutionStudentManager,int>(Context("admin",true),m=>m.CountAsync()));
    }

    [Fact]
    public async Task MissingStudentProfileAndNonHttpScopeFailClosed()
    {
        await Execute("DELETE FROM institution_student_profile WHERE membership_id=@m",("@m",Id("student-membership")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Call<IInstitutionSubjectManager,List<SubjectDto>>(Context("student"),m=>m.GetAllAsync()));
        using var noAccessor=fixture.ScopeFactory.CreateScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>noAccessor.ServiceProvider.GetRequiredService<IInstitutionStudentManager>().GetAllAsync());
    }

    [Fact]
    public async Task DeleteBusinessRulesAndScopedNameUniquenessRemainEffective()
    {
        foreach(var family in new[]{"teacher","subject"})await Assert.ThrowsAsync<BusinessRuleException>(()=>Delete(family,Context(),Id(family)));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>Call<IInstitutionSubjectManager,Guid>(Context(),m=>m.AddAsync(new(){Name="Matematica"})));
        var unlinked=await Add("student",Context());var grade=Guid.NewGuid();
        await Execute("INSERT INTO grade(id,institution_id,student_id,class_subject_id,value,date) VALUES (@id,@a,@student,@cs,8,'2500-10-01')",("@id",grade),("@a",_a),("@student",unlinked),("@cs",Id("assignment")));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>Delete("student",Context(),unlinked));
        Assert.Equal(1L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM grade WHERE id=@id",("@id",grade))));
    }

    [Fact]
    public async Task ForeignKeysAndImmutableOwnerStillProtectManagerCreatedRecords()
    {
        var id=await Add("student",Context());
        var owner=await Assert.ThrowsAsync<MySqlException>(()=>Execute("UPDATE student SET institution_id=@b WHERE id=@id",("@b",_b),("@id",id)));Assert.Equal(1644,owner.Number);
        var link=await Assert.ThrowsAsync<MySqlException>(()=>Execute("UPDATE class_subject SET subject_id=@b WHERE id=@id",("@b",Id("subject",true)),("@id",Id("assignment"))));Assert.Equal(1452,link.Number);
        Assert.Equal(_a.ToString(),(await Scalar("SELECT institution_id FROM student WHERE id=@id",("@id",id)))!.ToString());
    }

    [Fact]
    public async Task ConcurrentScopedCallsAndTwoAdministratorsHaveNoContextOrOwnershipLeak()
    {
        var results=await Task.WhenAll(new[]{false,true}.Select(b=>Call<IInstitutionStudentManager,List<StudentDto>>(Context("admin",b),m=>m.GetAllAsync())));
        Assert.All(results[0],s=>Assert.Contains(s.Id,new[]{Id("student"),Id("student-other")}));Assert.All(results[1],s=>Assert.Contains(s.Id,new[]{Id("student",true),Id("student-other",true)}));
        var admin2=new TenantContext(_admin2,_a,Id("admin2-membership"),new[]{InstitutionRole.InstitutionAdmin});
        var created=await Task.WhenAll(Add("student",Context()),Add("student",admin2));Assert.NotEqual(created[0],created[1]);
        foreach(var id in created)Assert.Equal(_a.ToString(),(await Scalar("SELECT institution_id FROM student WHERE id=@id",("@id",id)))!.ToString());
        var denied=Assert.ThrowsAsync<KeyNotFoundException>(()=>Update("teacher",Context(),Id("teacher",true)));
        await Task.WhenAll(denied,Update("teacher",Context("admin",true),Id("teacher",true)));
        Assert.Equal("Changed",(await Scalar("SELECT first_name FROM teacher WHERE id=@id",("@id",Id("teacher",true))))!.ToString());
    }

    [Fact]
    public async Task ConcurrentDuplicateSubjectIsOneSuccessAndOneBusinessError()
    {
        var name="Concurrent "+Guid.NewGuid().ToString("N");var admin2=new TenantContext(_admin2,_a,Id("admin2-membership"),new[]{InstitutionRole.InstitutionAdmin});
        async Task<bool> Create(TenantContext t) {try{await Call<IInstitutionSubjectManager,Guid>(t,m=>m.AddAsync(new(){Name=name}));return true;}catch(BusinessRuleException){return false;}}
        var outcomes=await Task.WhenAll(Create(Context()),Create(admin2));Assert.Single(outcomes.Where(x=>x));Assert.Single(outcomes.Where(x=>!x));
    }

    [Fact]
    public async Task CapturedQueriesFilterInSqlHaveServerCountAndNoListNPlusOne()
    {
        _sql.Clear();await Call<IInstitutionStudentManager,List<StudentDto>>(Context("teacher"),m=>m.GetAllAsync());
        Assert.InRange(_sql.Commands.Count,1,4);var student=_sql.Commands.Last().Sql;Assert.Contains("institution_id",student);Assert.Contains("EXISTS",student,StringComparison.OrdinalIgnoreCase);Assert.Contains("teacher_id",student);
        _sql.Clear();await Call<IInstitutionStudentManager,int>(Context(),m=>m.CountAsync());Assert.Contains("COUNT(*)",_sql.Commands.Last().Sql);
        _sql.Clear();await Call<IInstitutionSubjectManager,List<SubjectDto>>(Context("student"),m=>m.GetAllAsync());Assert.InRange(_sql.Commands.Count,1,4);Assert.Contains("class_id",_sql.Commands.Last().Sql);Assert.Contains("institution_id",_sql.Commands.Last().Sql);
        var directory=Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","Backend","ElectronicRegisterAPI","mt3b-evidence");Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"generated-subject-query.sql.txt"),_sql.Commands.Last().Sql);
        using var scope=_provider.CreateScope();scope.ServiceProvider.GetRequiredService<TestCurrent>().Set(await PersistedTestTenant.ReadAsync(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>(),Context()));
        await scope.ServiceProvider.GetRequiredService<IInstitutionTeacherManager>().GetAllAsync();Assert.Empty(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().ChangeTracker.Entries());
        Assert.Equal("StudentManager",scope.ServiceProvider.GetRequiredService<IStudentManager>().GetType().Name);
    }

    private Task<Guid> Add(string family,TenantContext? t) => family switch {
        "student"=>Call<IInstitutionStudentManager,Guid>(t,m=>m.AddAsync(new(){FirstName="Fresh",LastName="Student"})),
        "teacher"=>Call<IInstitutionTeacherManager,Guid>(t,m=>m.AddAsync(new(){FirstName="Fresh",LastName="Teacher"})),
        _=>Call<IInstitutionSubjectManager,Guid>(t,m=>m.AddAsync(new(){Name="Fresh "+Guid.NewGuid().ToString("N")}))};
    private Task Update(string family,TenantContext t,Guid id)=>family switch {
        "student"=>Call<IInstitutionStudentManager>(t,m=>m.UpdateAsync(id,new(){FirstName="Changed",LastName="Student"})),
        "teacher"=>Call<IInstitutionTeacherManager>(t,m=>m.UpdateAsync(id,new(){FirstName="Changed",LastName="Teacher"})),
        _=>Call<IInstitutionSubjectManager>(t,m=>m.UpdateAsync(id,new(){Name="Changed"}))};
    private Task Delete(string family,TenantContext t,Guid id)=>family switch {
        "student"=>Call<IInstitutionStudentManager>(t,m=>m.DeleteAsync(id)),"teacher"=>Call<IInstitutionTeacherManager>(t,m=>m.DeleteAsync(id)),_=>Call<IInstitutionSubjectManager>(t,m=>m.DeleteAsync(id))};
    private async Task<string> TenantDigest(Guid tenant)
    {
        await using var c=await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);var rows=new List<string>();
        foreach(var table in new[]{"student","teacher","subject","class_subject","grade"})
        { await using var cmd=new MySqlCommand($"SELECT * FROM `{table}` WHERE institution_id=@i ORDER BY id",c);cmd.Parameters.AddWithValue("@i",tenant);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync()){var cells=new object[r.FieldCount];r.GetValues(cells);rows.Add(System.Text.Json.JsonSerializer.Serialize(cells));} }
        return string.Join("\n",rows);
    }
    public async Task DisposeAsync()
    {
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
        if(_provider!=null)await _provider.DisposeAsync();
    }
    private sealed class TestCurrent : ICurrentTenant
    {
        private TenantContext? _value;
        public TenantContext Value=>_value??throw new UnauthorizedAccessException("Test scope has no verified context.");
        public void Set(TenantContext value){if(_value!=null)throw new InvalidOperationException("Scope already initialized.");_value=value;}
    }
    private sealed record Command(string Sql,IReadOnlyDictionary<string,Guid> Guids);
    private sealed class Capture : DbCommandInterceptor
    {
        public ConcurrentQueue<Command> Commands {get;}=new();
        public void Clear()=>Commands.Clear();
        private void Record(DbCommand cmd){var ids=new Dictionary<string,Guid>();foreach(DbParameter p in cmd.Parameters)if(Guid.TryParse(p.Value?.ToString(),out var id))ids[p.ParameterName]=id;Commands.Enqueue(new(cmd.CommandText,ids));}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c,CommandEventData d,InterceptionResult<DbDataReader> r,CancellationToken ct=default){Record(c);return new(r);}
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c,CommandEventData d,InterceptionResult<int> r,CancellationToken ct=default){Record(c);return new(r);}
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c,CommandEventData d,InterceptionResult<object> r,CancellationToken ct=default){Record(c);return new(r);}
    }
}
