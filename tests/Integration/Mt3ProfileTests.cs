using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mt3ProfileTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private readonly Guid _a=Guid.NewGuid(), _b=Guid.NewGuid(), _user=Guid.NewGuid(), _ma=Guid.NewGuid(), _mb=Guid.NewGuid();
    private readonly Guid _ta=Guid.NewGuid(), _tb=Guid.NewGuid(), _sa=Guid.NewGuid(), _sb=Guid.NewGuid(), _legacy=Guid.NewGuid();
    private readonly Guid _legacyTeacher=Guid.NewGuid(), _legacyUser=Guid.NewGuid();
    private TenantContext Context(bool b=false) => new(_user,b?_b:_a,b?_mb:_ma,new[]{InstitutionRole.Teacher,InstitutionRole.Student});
    private async Task Db(Func<ElectronicRegisterContext,Task> action)
    { using var scope=fixture.ScopeFactory.CreateScope(); await action(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>()); }
    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);
        await Db(async db =>
        {
            foreach(var id in new[]{_a,_b}) db.Institutions.Add(new E.Institution{Id=id,Name="Same institution name",Code="MT3_"+id.ToString("N"),Status="active",CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow});
            await db.SaveChangesAsync();
        });
        await Db(async db =>
        {
            foreach(var id in new[]{_ta,_tb}) db.Teachers.Add(new E.Teacher{Id=id,FirstName="Same",LastName="Teacher"});
            foreach(var id in new[]{_sa,_sb}) db.Students.Add(new E.Student{Id=id,FirstName="Same",LastName="Student"});
            foreach(var entry in db.ChangeTracker.Entries())
                entry.Property("InstitutionId").CurrentValue = entry.Entity switch { E.Teacher t => t.Id==_ta?_a:_b, E.Student s => s.Id==_sa?_a:_b, _ => throw new InvalidOperationException() };
            db.Users.Add(new E.User{Id=_user,Email=_user.ToString("N")+"@mt3.example.test",PasswordHash="",Role="teacher",TeacherId=_ta});
            db.InstitutionMemberships.AddRange(Membership(_ma,_a),Membership(_mb,_b));
            db.InstitutionTeacherProfiles.AddRange(new(){InstitutionId=_a,MembershipId=_ma,TeacherId=_ta},new(){InstitutionId=_b,MembershipId=_mb,TeacherId=_tb});
            db.InstitutionStudentProfiles.AddRange(new(){InstitutionId=_a,MembershipId=_ma,StudentId=_sa},new(){InstitutionId=_b,MembershipId=_mb,StudentId=_sb});
            await db.SaveChangesAsync();
        });
    }
    private E.InstitutionMembership Membership(Guid id,Guid institution) => new(){Id=id,InstitutionId=institution,UserId=_user,Status="active",JoinedAt=DateTime.UtcNow,
        InstitutionMembershipRoles=new List<E.InstitutionMembershipRole>{new(){MembershipId=id,Role="Teacher"},new(){MembershipId=id,Role="Student"}}};
    private async Task<Guid> Resolve(bool teacher,bool b=false,TenantContext? context=null)
    { using var scope=fixture.ScopeFactory.CreateScope(); var resolver=scope.ServiceProvider.GetRequiredService<IInstitutionProfileResolver>();
      var tenant=await PersistedTestTenant.ReadAsync(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>(),context??Context(b));
      return teacher ? await resolver.RequireTeacherAsync(tenant) : await resolver.RequireStudentAsync(tenant); }

    [Fact]
    public async Task GlobalIdentityResolvesDifferentProfilesAndBothRolesInEachFoundation()
    {
        Assert.Equal(_ta,await Resolve(true)); Assert.Equal(_tb,await Resolve(true,true));
        Assert.Equal(_sa,await Resolve(false)); Assert.Equal(_sb,await Resolve(false,true));
        await Db(async db=>{var user=await db.Users.AsNoTracking().SingleAsync(u=>u.Id==_user);Assert.Equal(_ta,user.TeacherId);Assert.Null(user.StudentId);});
        var results=await Task.WhenAll(Resolve(true),Resolve(true,true)); Assert.Equal(new[]{_ta,_tb},results);
    }
    [Theory]
    [InlineData("wrong-user")]
    [InlineData("wrong-institution")]
    [InlineData("wrong-membership")]
    [InlineData("admin-no-profile")]
    public async Task ContextCannotInventAProfile(string fault)
    {
        var token=new TenantContext(fault=="wrong-user"?Guid.NewGuid():_user,fault=="wrong-institution"?_b:_a,
            fault=="wrong-membership"?_mb:_ma,fault=="admin-no-profile"?new[]{InstitutionRole.InstitutionAdmin}:new[]{InstitutionRole.Teacher,InstitutionRole.Student});
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Resolve(true,context:token));
    }
    [Theory]
    [InlineData("membership")]
    [InlineData("institution")]
    [InlineData("role")]
    [InlineData("missing-profile")]
    public async Task RevocationOrMissingProfileNeverFallsBackToGlobalTeacherId(string change)
    {
        Assert.Equal(_tb,await Resolve(true,true));
        await Db(async db=>
        {
            if(change=="membership") await db.InstitutionMemberships.Where(m=>m.Id==_mb).ExecuteUpdateAsync(s=>s.SetProperty(m=>m.Status,"suspended"));
            if(change=="institution") await db.Institutions.Where(i=>i.Id==_b).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.Status,"suspended"));
            if(change=="role") await db.InstitutionMembershipRoles.Where(r=>r.MembershipId==_mb&&r.Role=="Teacher").ExecuteDeleteAsync();
            if(change=="missing-profile") await db.InstitutionTeacherProfiles.Where(p=>p.MembershipId==_mb).ExecuteDeleteAsync();
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Resolve(true,true));
        Assert.Equal(_ta,await Resolve(true));
    }
    [Theory]
    [InlineData(true,"membership",1452)]
    [InlineData(false,"membership",1452)]
    [InlineData(true,"duplicate",1062)]
    [InlineData(false,"duplicate",1062)]
    [InlineData(true,"missing",1452)]
    [InlineData(false,"missing",1452)]
    public async Task SqlRejectsCrossInstitutionMembershipDuplicateOwnershipAndInvalidReferences(bool teacher,string fault,int number)
    {
        await Db(async db=>
        {
            if(teacher) await db.InstitutionTeacherProfiles.Where(p=>p.MembershipId==_mb).ExecuteDeleteAsync();
            else await db.InstitutionStudentProfiles.Where(p=>p.MembershipId==_mb).ExecuteDeleteAsync();
            if(teacher) db.InstitutionTeacherProfiles.Add(new(){InstitutionId=fault=="membership"?_a:_b,MembershipId=_mb,TeacherId=fault=="missing"?Guid.NewGuid():_ta});
            else db.InstitutionStudentProfiles.Add(new(){InstitutionId=fault=="membership"?_a:_b,MembershipId=_mb,StudentId=fault=="missing"?Guid.NewGuid():_sa});
            var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
            Assert.Equal(number,Assert.IsType<MySqlException>(error.InnerException).Number);
        });
        await Db(async db=>Assert.Equal(_ta,(await db.InstitutionTeacherProfiles.AsNoTracking().SingleAsync(p=>p.MembershipId==_ma)).TeacherId));
    }
    [Fact]
    public async Task ControlledProfileBackfillPreservesEveryLegacyCellAndIsIdempotent()
    {
        await Db(async db=>
        {
            await db.InstitutionTeacherProfiles.Where(p=>p.MembershipId==_ma||p.MembershipId==_mb).ExecuteDeleteAsync();
            await db.InstitutionStudentProfiles.Where(p=>p.MembershipId==_ma||p.MembershipId==_mb).ExecuteDeleteAsync();
        });
        await Mt1SchemaMigration.BackfillAsync(fixture.ConnectionString,_legacy,"MT3 controlled legacy holding - not a legal identity");
        // MT3A: A/B profiles are already institution-owned. The legacy backfill must
        // link a legacy-owned profile, never move A's profile to the holding.
        await Db(async db=>
        {
            var teacher=new E.Teacher{Id=_legacyTeacher,FirstName="Legacy",LastName="Without invented identity"};
            db.Teachers.Add(teacher);db.Entry(teacher).Property("InstitutionId").CurrentValue=_legacy;
            db.Users.Add(new E.User{Id=_legacyUser,Email=_legacyUser.ToString("N")+"@mt3.example.test",PasswordHash="",Role="teacher",TeacherId=_legacyTeacher});
            await db.SaveChangesAsync();
        });
        await Mt1SchemaMigration.BackfillAsync(fixture.ConnectionString,_legacy,"MT3 controlled legacy holding - not a legal identity");
        var before=await Snapshot();
        await Mt3ProfileBackfill.ExecuteAsync(fixture.ConnectionString,_legacy);
        Assert.Equal(before,await Snapshot());
        await Db(async db=>
        {
            var profile=await db.InstitutionTeacherProfiles.AsNoTracking().SingleAsync(p=>p.InstitutionId==_legacy&&p.TeacherId==_legacyTeacher);
            Assert.Equal(_legacyUser,(await db.InstitutionMemberships.AsNoTracking().SingleAsync(m=>m.Id==profile.MembershipId)).UserId);
            Assert.False(await db.InstitutionTeacherProfiles.AnyAsync(p=>p.InstitutionId==_legacy&&p.TeacherId==_ta));
        });
        await Mt3ProfileBackfill.ExecuteAsync(fixture.ConnectionString,_legacy);
        Assert.Equal(before,await Snapshot());
    }
    [Fact]
    public async Task ConflictingOwnershipRollsBackEntireBackfill()
    {
        var before=await Snapshot();
        await Mt1SchemaMigration.BackfillAsync(fixture.ConnectionString,_legacy,"MT3 controlled legacy holding - not a legal identity");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Mt3ProfileBackfill.ExecuteAsync(fixture.ConnectionString,_legacy));
        Assert.Equal(before,await Snapshot());
        await Db(async db=>{Assert.False(await db.InstitutionTeacherProfiles.AnyAsync(p=>p.InstitutionId==_legacy));Assert.False(await db.InstitutionStudentProfiles.AnyAsync(p=>p.InstitutionId==_legacy));});
    }
    [Theory]
    [InlineData("other_database")]
    [InlineData("electronic_register")]
    public async Task MigrationRejectsAnyOtherDatabaseBeforeConnecting(string database) =>
        await Assert.ThrowsAsync<ArgumentException>(()=>Mt3ProfileBackfill.ExecuteAsync("Server=not-a-server;Database="+database+";User=x;",_legacy));

    [Fact]
    public async Task IncompatibleLegacyRoleBlocksBackfillWithoutChangingAnyLegacyCell()
    {
        await Db(async db=>await db.Users.Where(u=>u.Id==_user).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.Role,"admin")));
        var before=await Snapshot();
        await Mt1SchemaMigration.BackfillAsync(fixture.ConnectionString,_legacy,"MT3 controlled legacy holding - not a legal identity");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Mt3ProfileBackfill.ExecuteAsync(fixture.ConnectionString,_legacy));
        Assert.Equal(before,await Snapshot());
    }

    [Fact]
    public async Task RealAdminMembershipDoesNotImplyTeacherOrStudentProfile()
    {
        await Db(async db=>
        {
            await db.InstitutionMembershipRoles.Where(r=>r.MembershipId==_mb).ExecuteDeleteAsync();
            db.InstitutionMembershipRoles.Add(new(){MembershipId=_mb,Role="InstitutionAdmin"});
            await db.InstitutionTeacherProfiles.Where(p=>p.MembershipId==_mb).ExecuteDeleteAsync();
            await db.InstitutionStudentProfiles.Where(p=>p.MembershipId==_mb).ExecuteDeleteAsync();
            await db.SaveChangesAsync();
        });
        var admin=new TenantContext(_user,_b,_mb,new[]{InstitutionRole.InstitutionAdmin});
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Resolve(true,context:admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Resolve(false,context:admin));
    }

    private async Task<string> Snapshot()
    {
        var tables=new[]{"audit_event","biennium","biennium_study_area","biennium_study_path","class","class_subject","exam","exam_student","exam_submission","exam_violation","grade","question","question_option","student","study_area","study_path","subject","submission_answer","submission_answer_option","teacher","user"};
        var result=new SortedDictionary<string,object>();
        await using var connection=new MySqlConnection(fixture.ConnectionString);await connection.OpenAsync();
        foreach(var table in tables)
        {
            var rows=new List<string>();await using var command=new MySqlCommand("SELECT * FROM `"+table+"`",connection);await using var reader=await command.ExecuteReaderAsync();
            while(await reader.ReadAsync())
            {
                var fields=new List<string?>();for(int i=0;i<reader.FieldCount;i++) fields.Add(reader.IsDBNull(i)?null:reader.GetValue(i) switch
                    {DateTime date=>date.ToString("O",CultureInfo.InvariantCulture),byte[] bytes=>Convert.ToHexString(bytes),IFormattable value=>value.ToString(null,CultureInfo.InvariantCulture),var value=>value.ToString()});
                rows.Add(JsonSerializer.Serialize(fields));
            }
            rows.Sort(StringComparer.Ordinal);result[table]=new{Count=rows.Count,Sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows))))};
        }
        return JsonSerializer.Serialize(result);
    }
    public async Task DisposeAsync()
    {
        await Db(async db=>
        {
            var institutions=new[]{_a,_b,_legacy};
            await using var purge = await MembershipFixturePurge.AllowAsync(db, institutions);
            await db.Institutions.Where(i => i.Id == _legacy && i.Code == Mt1SchemaMigration.LegacyMigrationCode &&
                i.Name == "MT3 controlled legacy holding - not a legal identity")
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Code, "MT3_REHEARSAL_" + _legacy.ToString("N")));
            await db.InstitutionTeacherProfiles.Where(p=>institutions.Contains(p.InstitutionId)).ExecuteDeleteAsync();
            await db.InstitutionStudentProfiles.Where(p=>institutions.Contains(p.InstitutionId)).ExecuteDeleteAsync();
            var memberships=await db.InstitutionMemberships.Where(m=>institutions.Contains(m.InstitutionId)).Select(m=>m.Id).ToListAsync();
            await db.InstitutionMembershipRoles.Where(r=>memberships.Contains(r.MembershipId)).ExecuteDeleteAsync();
            await db.InstitutionMemberships.Where(m=>institutions.Contains(m.InstitutionId)).ExecuteDeleteAsync();
            await db.Users.Where(u=>u.Id==_user||u.Id==_legacyUser).ExecuteDeleteAsync();
            await db.Teachers.Where(t=>t.Id==_legacyTeacher).ExecuteDeleteAsync();
            await db.Teachers.Where(t=>t.Id==_ta||t.Id==_tb).ExecuteDeleteAsync();
            await db.Students.Where(s=>s.Id==_sa||s.Id==_sb).ExecuteDeleteAsync();
            await db.Institutions.Where(i=>institutions.Contains(i.Id)).ExecuteDeleteAsync();
        });
    }
}
