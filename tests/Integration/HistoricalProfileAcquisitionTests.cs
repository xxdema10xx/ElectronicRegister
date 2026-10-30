using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs.Membership;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using E=ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class HistoricalProfileAcquisitionTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private readonly Guid _institution=Guid.NewGuid(),_foreign=Guid.NewGuid(),_admin=Guid.NewGuid(),_target=Guid.NewGuid(),_staff=Guid.NewGuid();
    private readonly Guid _adminMember=Guid.NewGuid(),_targetMember=Guid.NewGuid(),_staffMember=Guid.NewGuid();
    private readonly Guid _student=Guid.NewGuid(),_teacher=Guid.NewGuid(),_foreignStudent=Guid.NewGuid();
    private Mt3cHosting.ProgramHost _host=null!;

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);
        using var scope=fixture.ScopeFactory.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        foreach(var id in new[]{_institution,_foreign}) db.Institutions.Add(new(){Id=id,Code="ACQ_"+id.ToString("N"),Name="Explicit acquisition fixture",Status="active",CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow});
        foreach(var id in new[]{_admin,_target,_staff}) db.Users.Add(new(){Id=id,Email=id.ToString("N")+"@acquisition.example.test",PasswordHash="",Role="admin"});
        E.InstitutionMembership Member(Guid id,Guid user,params string[] roles)=>new(){Id=id,UserId=user,InstitutionId=_institution,Status="active",JoinedAt=DateTime.UtcNow,InstitutionMembershipRoles=roles.Select(role=>new E.InstitutionMembershipRole{MembershipId=id,Role=role}).ToArray()};
        db.InstitutionMemberships.AddRange(Member(_adminMember,_admin,"InstitutionAdmin"),Member(_targetMember,_target,"Student","Teacher"),Member(_staffMember,_staff,"Staff"));
        var student=new E.Student{Id=_student,FirstName="Historical",LastName="Student"};
        var teacher=new E.Teacher{Id=_teacher,FirstName="Historical",LastName="Teacher"};
        var foreign=new E.Student{Id=_foreignStudent,FirstName="Foreign",LastName="Student"};
        db.Students.AddRange(student,foreign);db.Teachers.Add(teacher);
        db.Entry(student).Property("InstitutionId").CurrentValue=_institution;
        db.Entry(teacher).Property("InstitutionId").CurrentValue=_institution;
        db.Entry(foreign).Property("InstitutionId").CurrentValue=_foreign;
        await db.SaveChangesAsync();
        _host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_institution,Array.Empty<Guid>(),enabled:false,school:true,membershipAdmin:true,membershipWrite:true);
    }

    public async Task DisposeAsync()=>await _host.DisposeAsync();

    private async Task<TenantContext> Context(bool staff=false)
    {
        using var scope=fixture.ScopeFactory.CreateScope();
        return await PersistedTestTenant.ReadAsync(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>(),new(staff?_staff:_admin,_institution,staff?_staffMember:_adminMember,new[]{staff?InstitutionRole.Staff:InstitutionRole.InstitutionAdmin}));
    }

    private async Task<string> Token(bool staff=false)
    {
        using var scope=_host.Host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IInstitutionTokens>().Access(await Context(staff));
    }

    private AcquireHistoricalProfile Input(string kind="Student")=>new(Guid.NewGuid(),0,kind=="Student"?_student:_teacher,kind,"Explicit reviewed historical association");

    private async Task<AcquireHistoricalProfile> Prepared(string kind)
    {
        using var scope=fixture.ScopeFactory.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var user=await db.Users.SingleAsync(u=>u.Id==_target);
        if(kind=="Student")user.StudentId=_student;else user.TeacherId=_teacher;
        await db.SaveChangesAsync();
        var revision=await db.InstitutionMembershipSecurityStates.Where(s=>s.MembershipId==_targetMember).Select(s=>s.SecurityGeneration).SingleAsync();
        return Input(kind) with{ExpectedRevision=revision};
    }

    private async Task<HttpResponseMessage> Send(object input,bool staff=false,Guid? target=null)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,$"/api/institution/admin/members/{target??_targetMember}/profile-acquisitions");
        request.Headers.Authorization=new("Bearer",await Token(staff));
        request.Content=JsonContent.Create(input);
        return await _host.Client.SendAsync(request);
    }

    private async Task<long> Events()
    {
        using var scope=fixture.ScopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Database.SqlQuery<long>($"SELECT COUNT(*) AS Value FROM institution_profile_acquisition WHERE institution_id={_institution}").SingleAsync();
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    public async Task ExplicitOwnedHistoricalLinkIsIdempotentAndPreservesCredentialsAndRole(string kind)
    {
        var input=await Prepared(kind);
        var first=await Send(input);Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var result=await first.Content.ReadFromJsonAsync<ProfileAcquisitionResult>();Assert.Equal("linked",result!.Status);
        var replay=await Send(input);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);Assert.Equal(result,await replay.Content.ReadFromJsonAsync<ProfileAcquisitionResult>());
        Assert.Equal(1,await Events());
        using var scope=fixture.ScopeFactory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal("admin",(await db.Users.AsNoTracking().SingleAsync(u=>u.Id==_target)).Role);
        Assert.Equal("",(await db.Users.AsNoTracking().SingleAsync(u=>u.Id==_target)).PasswordHash);
        Assert.Equal(input.ExpectedRevision,await db.InstitutionMembershipSecurityStates.Where(s=>s.MembershipId==_targetMember).Select(s=>s.SecurityGeneration).SingleAsync());
        if(kind=="Student")Assert.Equal(_student,await db.InstitutionStudentProfiles.Where(p=>p.MembershipId==_targetMember).Select(p=>p.StudentId).SingleAsync());
        else Assert.Equal(_teacher,await db.InstitutionTeacherProfiles.Where(p=>p.MembershipId==_targetMember).Select(p=>p.TeacherId).SingleAsync());
        await Assert.ThrowsAnyAsync<Exception>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM institution_profile_acquisition WHERE institution_id={_institution}"));
    }

    [Fact]
    public async Task MissingHistoricalProofCreatesReviewRecordWithoutInventingLinkOrMembership()
    {
        var input=await Prepared("Student");
        using(var scope=fixture.ScopeFactory.CreateScope())await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Users.Where(u=>u.Id==_target).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.StudentId,(Guid?)null));
        var response=await Send(input);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal("requires-review",(await response.Content.ReadFromJsonAsync<ProfileAcquisitionResult>())!.Status);
        using var verify=fixture.ScopeFactory.CreateScope();var db=verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.False(await db.InstitutionStudentProfiles.AnyAsync(p=>p.MembershipId==_targetMember));
        Assert.Equal(1,await db.InstitutionMemberships.CountAsync(m=>m.UserId==_target));
        Assert.Equal(1,await Events());
    }

    [Fact]
    public async Task ReviewQueueIsScopedPaginatedAndAdminOnly()
    {
        var input=await Prepared("Student");
        using(var scope=fixture.ScopeFactory.CreateScope())await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Users.Where(u=>u.Id==_target).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.StudentId,(Guid?)null));
        Assert.Equal(HttpStatusCode.OK,(await Send(input)).StatusCode);
        async Task<HttpResponseMessage> Read(string path,bool staff=false)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,path);
            request.Headers.Authorization=new("Bearer",await Token(staff));
            return await _host.Client.SendAsync(request);
        }
        var route=$"/api/institution/admin/members/{_targetMember}/profile-acquisitions";
        var response=await Read(route+"?status=requires-review");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal(input.EventId,Assert.Single((await response.Content.ReadFromJsonAsync<ProfileAcquisitionResult[]>())!).EventId);
        Assert.Empty((await (await Read(route+"?status=linked")).Content.ReadFromJsonAsync<ProfileAcquisitionResult[]>())!);
        Assert.Equal(HttpStatusCode.Forbidden,(await Read(route,true)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Read($"/api/institution/admin/members/{Guid.NewGuid()}/profile-acquisitions")).StatusCode);
        foreach(var query in new[]{"?status=unknown","?status=","?page=0","?size=101"}) Assert.Equal(HttpStatusCode.BadRequest,(await Read(route+query)).StatusCode);
    }

    [Theory]
    [InlineData("foreign-profile",HttpStatusCode.NotFound)]
    [InlineData("foreign-membership",HttpStatusCode.NotFound)]
    [InlineData("staff",HttpStatusCode.Forbidden)]
    [InlineData("stale-target",HttpStatusCode.Conflict)]
    [InlineData("unknown-field",HttpStatusCode.BadRequest)]
    public async Task CrossTenantOrUnauthorizedAcquisitionCannotMutate(string fault,HttpStatusCode expected)
    {
        var input=await Prepared("Student");
        if(fault=="foreign-profile")input=input with{ProfileId=_foreignStudent};
        if(fault=="stale-target")input=input with{ExpectedRevision=input.ExpectedRevision+1};
        object body=fault=="unknown-field"?new{input.EventId,input.ExpectedRevision,input.ProfileId,input.Kind,input.Reason,userId=_admin}:input;
        var response=await Send(body,fault=="staff",fault=="foreign-membership"?Guid.NewGuid():null);
        Assert.Equal(expected,response.StatusCode);Assert.Equal(0,await Events());
        using var scope=fixture.ScopeFactory.CreateScope();Assert.False(await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().InstitutionStudentProfiles.AnyAsync(p=>p.MembershipId==_targetMember));
    }

    [Fact]
    public async Task ConcurrentSameIntentCommitsOneLedgerEventAndConflictingReuseIsRejected()
    {
        var input=await Prepared("Student");
        var responses=await Task.WhenAll(Send(input),Send(input));Assert.All(responses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
        Assert.Equal(1,await Events());
        Assert.Equal(HttpStatusCode.Conflict,(await Send(input with{Reason="Conflicting intent"})).StatusCode);
        Assert.Equal(1,await Events());
    }

    [Fact]
    public async Task ExistingAssociationToAnotherMembershipRequiresReviewAndIsNeverReassigned()
    {
        var input=await Prepared("Student");
        var otherUser=Guid.NewGuid();var otherMember=Guid.NewGuid();
        using(var scope=fixture.ScopeFactory.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            db.Users.Add(new(){Id=otherUser,Email=otherUser.ToString("N")+"@acquisition.example.test",Role="student",PasswordHash=""});
            db.InstitutionMemberships.Add(new(){Id=otherMember,UserId=otherUser,InstitutionId=_institution,Status="active",JoinedAt=DateTime.UtcNow,InstitutionMembershipRoles=new[]{new E.InstitutionMembershipRole{MembershipId=otherMember,Role="Student"}}});
            await db.SaveChangesAsync();
            db.InstitutionStudentProfiles.Add(new(){InstitutionId=_institution,MembershipId=otherMember,StudentId=_student});
            await db.SaveChangesAsync();
        }
        var response=await Send(input);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal("requires-review",(await response.Content.ReadFromJsonAsync<ProfileAcquisitionResult>())!.Status);
        using var verify=fixture.ScopeFactory.CreateScope();var check=verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal(otherMember,await check.InstitutionStudentProfiles.Where(p=>p.StudentId==_student).Select(p=>p.MembershipId).SingleAsync());
        Assert.False(await check.InstitutionStudentProfiles.AnyAsync(p=>p.MembershipId==_targetMember));
        Assert.Equal(_student,(await check.Users.AsNoTracking().SingleAsync(u=>u.Id==_target)).StudentId);
    }

    [Fact]
    public async Task FailureAfterProfileInsertRollsBackBothLinkAndLedger()
    {
        var input=await Prepared("Student");
        await using var provider=Phase3.Provider(fixture,null,new AcquisitionFailure());
        using var scope=provider.CreateScope();
        var actor=await Context();
        await Assert.ThrowsAsync<Phase3.InjectedFailure>(()=>scope.ServiceProvider.GetRequiredService<IInstitutionMembershipMutationRepository>().AcquireProfileAsync(actor,_targetMember,input));
        using var verify=fixture.ScopeFactory.CreateScope();Assert.False(await verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().InstitutionStudentProfiles.AnyAsync(p=>p.MembershipId==_targetMember));Assert.Equal(0,await Events());
    }

    private sealed class AcquisitionFailure : DbCommandInterceptor
    {
        public override ValueTask<int> NonQueryExecutedAsync(System.Data.Common.DbCommand command,CommandExecutedEventData data,int result,CancellationToken cancellationToken=default)
        {
            if(command.CommandText.Contains("INSERT INTO institution_profile_acquisition"))throw new Phase3.InjectedFailure();
            return ValueTask.FromResult(result);
        }
    }
}
