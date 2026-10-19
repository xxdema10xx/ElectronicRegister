using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mt4aCourseStructureTests(IntegrationFixture fixture)
{
    private sealed class Graph
    {
        public Guid Owner=Guid.NewGuid(), Path=Guid.NewGuid(), Area=Guid.NewGuid(), Biennium=Guid.NewGuid(), Bsa=Guid.NewGuid(), Bsp=Guid.NewGuid(), Class=Guid.NewGuid(), Student=Guid.NewGuid(), Campus=Guid.NewGuid(), Edition=Guid.NewGuid();
        public TenantContext Tenant=>new(Guid.NewGuid(),Owner,Guid.NewGuid(),new[]{InstitutionRole.InstitutionAdmin});
    }
    private async Task Sql(string sql, params (string,object?)[] values)
    {
        await using var c=new MySqlConnection(fixture.ConnectionString); await c.OpenAsync(); await TestDatabaseSession.ConfigureAsync(c);
        await using var cmd=new MySqlCommand(sql,c); foreach(var (name,value) in values)cmd.Parameters.AddWithValue(name,value??DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
    private async Task Setup(Graph g)
    {
        await Sql("INSERT INTO institution(id,code,name,status) VALUES(@id,@code,'MT4 temporary','active')",("@id",g.Owner),("@code","MT4_"+g.Owner.ToString("N")));
        await Sql("INSERT INTO study_path(id,institution_id,name) VALUES(@id,@o,'Path')",("@id",g.Path),("@o",g.Owner));
        await Sql("INSERT INTO study_area(id,institution_id,name,description) VALUES(@id,@o,'Area','MT4 temporary area')",("@id",g.Area),("@o",g.Owner));
        await Sql("INSERT INTO biennium(id,institution_id,start_year,end_year) VALUES(@id,@o,'2025-01-01','2027-01-01')",("@id",g.Biennium),("@o",g.Owner));
        await Sql("INSERT INTO biennium_study_area(id,institution_id,biennium_id,study_area_id) VALUES(@id,@o,@b,@a)",("@id",g.Bsa),("@o",g.Owner),("@b",g.Biennium),("@a",g.Area));
        await Sql("INSERT INTO biennium_study_path(id,institution_id,biennium_study_area_id,study_path_id) VALUES(@id,@o,@b,@p)",("@id",g.Bsp),("@o",g.Owner),("@b",g.Bsa),("@p",g.Path));
        await Sql("INSERT INTO class(id,institution_id,biennium_study_path_id,name) VALUES(@id,@o,@b,'Group')",("@id",g.Class),("@o",g.Owner),("@b",g.Bsp));
        await Sql("INSERT INTO student(id,institution_id,class_id,first_name,last_name) VALUES(@id,@o,@c,'Test','MT4')",("@id",g.Student),("@o",g.Owner),("@c",g.Class));
        await Sql("INSERT INTO campus(id,institution_id,name) VALUES(@id,@o,'Campus')",("@id",g.Campus),("@o",g.Owner));
        await Repo(r=>r.AddEditionAsync(g.Tenant,Edition(g)));
    }
    private static CourseEdition Edition(Graph g,Guid? id=null)=>new(id??g.Edition,g.Owner,g.Path,"LOCAL","Edition",null,null,"planned");
    private static StudentEnrollment Enrollment(Graph g)=>new(Guid.NewGuid(),g.Owner,g.Student,g.Edition,new DateOnly(2026,10,19),null,"enrolled");
    private async Task Repo(Func<ICourseStructureRepository,Task> action)
    { using var scope=fixture.ScopeFactory.CreateScope();await action(scope.ServiceProvider.GetRequiredService<ICourseStructureRepository>()); }
    private async Task Pair(Func<Graph,Graph,Task> action)
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);var a=new Graph();var b=new Graph();
        try {await Setup(a);await Setup(b);await action(a,b);}
        finally {await Cleanup(a);await Cleanup(b);}
    }
    private async Task Cleanup(Graph g)
    {
        foreach(var table in new[]{"student_enrollment","course_edition_campus","student","class","course_edition","campus","biennium_study_path","biennium_study_area","biennium","study_area","study_path"})
            await Sql("DELETE FROM `"+table+"` WHERE institution_id=@o",("@o",g.Owner));
        await Sql("DELETE FROM institution WHERE id=@o",("@o",g.Owner));
    }
    private async Task Rejected(string sql,params (string,object?)[] values)
    {var error=await Assert.ThrowsAsync<MySqlException>(()=>Sql(sql,values));Assert.Contains(error.Number,new[]{1451,1452,1062,1644});}

    [Fact] public Task OwnersCodesAndScopedQueries()=>Pair(async(a,b)=>{
        await Repo(async r=>{Assert.Equal(a.Owner,Assert.Single(await r.GetEditionsAsync(a.Tenant)).InstitutionId);Assert.Equal(b.Owner,Assert.Single(await r.GetEditionsAsync(b.Tenant)).InstitutionId);});
        await Assert.ThrowsAsync<DbUpdateException>(()=>Repo(r=>r.AddEditionAsync(a.Tenant,Edition(a,Guid.NewGuid()))));
    });
    [Theory][InlineData("edition-path")][InlineData("campus")][InlineData("student-a")][InlineData("student-b")][InlineData("class")]
    public Task CompositeForeignKeysRejectOtherTenant(string kind)=>Pair(async(a,b)=>{
        switch(kind){
        case "edition-path":await Rejected("INSERT INTO course_edition(id,institution_id,study_path_id,code,name,status) VALUES(@id,@o,@p,'X','X','planned')",("@id",Guid.NewGuid()),("@o",a.Owner),("@p",b.Path));break;
        case "campus":await Rejected("INSERT INTO course_edition_campus VALUES(@o,@e,@c)",("@o",a.Owner),("@e",a.Edition),("@c",b.Campus));break;
        case "class":await Rejected("UPDATE class SET course_edition_id=@e WHERE id=@id",("@e",b.Edition),("@id",a.Class));break;
        default:var s=kind=="student-a"?a:b;var e=kind=="student-a"?b:a;
        await Rejected("INSERT INTO student_enrollment(id,institution_id,student_id,course_edition_id,enrollment_date,status) VALUES(@id,@o,@s,@e,'2026-10-19','enrolled')",("@id",Guid.NewGuid()),("@o",e.Owner),("@s",s.Student),("@e",e.Edition));break;}
    });
    [Fact] public Task MultipleCampusesAndEditionsLoadWithEf()=>Pair(async(a,b)=>{
        var campus2=Guid.NewGuid();var edition2=Guid.NewGuid();
        await Sql("INSERT INTO campus(id,institution_id,name) VALUES(@id,@o,'Second')",("@id",campus2),("@o",a.Owner));
        await Repo(async r=>{await r.AddEditionAsync(a.Tenant,Edition(a,edition2) with {Code="SECOND"});await r.LinkCampusAsync(a.Tenant,a.Edition,a.Campus);await r.LinkCampusAsync(a.Tenant,a.Edition,campus2);await r.LinkCampusAsync(a.Tenant,edition2,a.Campus);Assert.Equal(2,(await r.GetCampusIdsAsync(a.Tenant,a.Edition)).Count);Assert.Empty(await r.GetCampusIdsAsync(b.Tenant,a.Edition));});
        using var scope=fixture.ScopeFactory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var rows=await db.CourseEditionCampuses.AsNoTracking().Where(x=>x.InstitutionId==a.Owner).Include(x=>x.Campus).Include(x=>x.CourseEdition).ToListAsync();
        Assert.Equal(3,rows.Count);Assert.All(rows,x=>{Assert.Equal(a.Owner,x.Campus.InstitutionId);Assert.Equal(a.Owner,x.CourseEdition.InstitutionId);});
        await Assert.ThrowsAsync<DbUpdateException>(()=>Repo(r=>r.LinkCampusAsync(a.Tenant,a.Edition,a.Campus)));
        await Rejected("DELETE FROM campus WHERE id=@id",("@id",a.Campus));
    });
    [Fact] public Task EnrollmentHistorySurvivesStatusAndClassIsUnchanged()=>Pair(async(a,b)=>{
        var e=Enrollment(a);var second=Guid.NewGuid();
        await Repo(async r=>{await r.AddEnrollmentAsync(a.Tenant,e);await r.SetEnrollmentStatusAsync(a.Tenant,e.Id,"withdrawn",new DateOnly(2026,10,20));await r.AddEditionAsync(a.Tenant,Edition(a,second) with {Code="SECOND"});await r.AddEnrollmentAsync(a.Tenant,Enrollment(a) with {CourseEditionId=second});});
        await Repo(async r=>{var rows=await r.GetEnrollmentsAsync(a.Tenant,a.Student);Assert.Equal(2,rows.Count);Assert.Equal("withdrawn",rows.Single(x=>x.Id==e.Id).Status);Assert.Empty(await r.GetEnrollmentsAsync(b.Tenant,a.Student));});
        await Sql("UPDATE course_edition SET status='completed' WHERE id=@id",("@id",a.Edition));
        await Repo(async r=>Assert.Equal(2,(await r.GetEnrollmentsAsync(a.Tenant,a.Student)).Count));
        using var scope=fixture.ScopeFactory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal(a.Class,(await db.Students.AsNoTracking().SingleAsync(x=>x.Id==a.Student)).ClassId);
        Assert.Equal(0,await db.InstitutionMemberships.CountAsync(x=>x.InstitutionId==a.Owner||x.InstitutionId==b.Owner));
        await Assert.ThrowsAsync<DbUpdateException>(()=>Repo(r=>r.AddEnrollmentAsync(a.Tenant,e with {Id=Guid.NewGuid()})));
        await Rejected("DELETE FROM student WHERE id=@id",("@id",a.Student));
        await Rejected("DELETE FROM course_edition WHERE id=@id",("@id",a.Edition));
    });
    [Theory][InlineData("institution_id")][InlineData("student_id")][InlineData("course_edition_id")][InlineData("enrollment_date")]
    public Task EnrollmentIdentityIsImmutable(string column)=>Pair(async(a,b)=>{
        var e=Enrollment(a);await Repo(r=>r.AddEnrollmentAsync(a.Tenant,e));
        object value=column=="institution_id"?b.Owner:column=="student_id"?b.Student:column=="course_edition_id"?b.Edition:"2026-10-20";
        await Rejected("UPDATE student_enrollment SET "+column+"=@v WHERE id=@id",("@v",value),("@id",e.Id));
        await Repo(async r=>Assert.Equal(a.Owner,Assert.Single(await r.GetEnrollmentsAsync(a.Tenant,a.Student)).InstitutionId));
    });
    [Theory][InlineData("edition-owner")][InlineData("edition-path")][InlineData("campus-owner")][InlineData("link-owner")]
    public Task OwnershipCannotBeTransferred(string kind)=>Pair(async(a,b)=>{
        await Repo(r=>r.LinkCampusAsync(a.Tenant,a.Edition,a.Campus));
        if(kind=="edition-path")await Rejected("UPDATE course_edition SET study_path_id=@v WHERE id=@id",("@v",b.Path),("@id",a.Edition));
        else if(kind=="edition-owner")await Rejected("UPDATE course_edition SET institution_id=@v WHERE id=@id",("@v",b.Owner),("@id",a.Edition));
        else if(kind=="campus-owner")await Rejected("UPDATE campus SET institution_id=@v WHERE id=@id",("@v",b.Owner),("@id",a.Campus));
        else await Rejected("UPDATE course_edition_campus SET institution_id=@v WHERE course_edition_id=@id",("@v",b.Owner),("@id",a.Edition));
    });
    [Theory][InlineData("edition-status")][InlineData("edition-dates")][InlineData("enrollment-status")][InlineData("enrollment-dates")][InlineData("blank-code")]
    public Task SqlValidatesStructure(string kind)=>Pair(async(a,b)=>{
        var e=Enrollment(a);await Repo(r=>r.AddEnrollmentAsync(a.Tenant,e));
        var sql=kind switch {"edition-status"=>"UPDATE course_edition SET status='unknown' WHERE id=@id","edition-dates"=>"UPDATE course_edition SET start_date='2027-01-01',end_date='2026-01-01' WHERE id=@id","blank-code"=>"UPDATE course_edition SET code=' ' WHERE id=@id","enrollment-status"=>"UPDATE student_enrollment SET status='unknown' WHERE id=@id",_=>"UPDATE student_enrollment SET completion_date='2020-01-01' WHERE id=@id"};
        await Rejected(sql,("@id",kind.StartsWith("enrollment")?e.Id:a.Edition));
    });
    [Fact] public Task ClassPathMustMatchAndLegacyRemainsNullable()=>Pair(async(a,b)=>{
        using(var scope=fixture.ScopeFactory.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();Assert.Null(await db.Classes.Where(x=>x.Id==a.Class).Select(x=>EF.Property<Guid?>(x,"CourseEditionId")).SingleAsync());}
        await Repo(r=>r.AssignClassAsync(a.Tenant,a.Class,a.Edition));
        await Rejected("DELETE FROM course_edition WHERE id=@id",("@id",a.Edition));
        var path2=Guid.NewGuid();await Sql("INSERT INTO study_path(id,institution_id,name) VALUES(@id,@o,'Other path')",("@id",path2),("@o",a.Owner));
        await Rejected("UPDATE biennium_study_path SET study_path_id=@p WHERE id=@id",("@p",path2),("@id",a.Bsp));
    });
    [Fact] public Task RepositoryRejectsForgedOwnerAndForeignResources()=>Pair(async(a,b)=>{
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Repo(r=>r.AddEditionAsync(a.Tenant,Edition(b,Guid.NewGuid()))));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Repo(r=>r.AddEnrollmentAsync(a.Tenant,Enrollment(a) with {CourseEditionId=b.Edition})));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Repo(r=>r.LinkCampusAsync(a.Tenant,a.Edition,b.Campus)));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Repo(r=>r.AssignClassAsync(a.Tenant,a.Class,b.Edition)));
    });
    [Fact] public Task ConcurrentScopesRemainSeparate()=>Pair(async(a,b)=>{
        await Task.WhenAll(Repo(r=>r.AddEnrollmentAsync(a.Tenant,Enrollment(a))),Repo(r=>r.AddEnrollmentAsync(b.Tenant,Enrollment(b))));
        await Task.WhenAll(Repo(async r=>Assert.Equal(a.Owner,Assert.Single(await r.GetEnrollmentsAsync(a.Tenant,a.Student)).InstitutionId)),Repo(async r=>Assert.Equal(b.Owner,Assert.Single(await r.GetEnrollmentsAsync(b.Tenant,b.Student)).InstitutionId)));
    });
    [Fact] public async Task InformationSchemaHasCompositeRestrictConstraints()
    {
        await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();
        await using var cmd=new MySqlCommand("SELECT constraint_name,COUNT(*) FROM information_schema.key_column_usage WHERE constraint_schema=DATABASE() AND LEFT(constraint_name,7)='fk_mt4_' AND referenced_table_name IS NOT NULL GROUP BY constraint_name",c);
        var counts=new Dictionary<string,int>();await using(var reader=await cmd.ExecuteReaderAsync()){while(await reader.ReadAsync())counts[reader.GetString(0)]=reader.GetInt32(1);}
        Assert.Equal(7,counts.Count);Assert.Equal(1,counts["fk_mt4_edition_owner"]);Assert.All(counts.Where(x=>x.Key!="fk_mt4_edition_owner"),x=>Assert.Equal(2,x.Value));
        cmd.CommandText="SELECT constraint_name,GROUP_CONCAT(CONCAT(table_name,'.',column_name,'->',referenced_table_name,'.',referenced_column_name) ORDER BY ordinal_position SEPARATOR ',') FROM information_schema.key_column_usage WHERE constraint_schema=DATABASE() AND LEFT(constraint_name,7)='fk_mt4_' AND referenced_table_name IS NOT NULL GROUP BY constraint_name";
        var mappings=new Dictionary<string,string>();await using(var reader=await cmd.ExecuteReaderAsync()){while(await reader.ReadAsync())mappings[reader.GetString(0)]=reader.GetString(1);}
        var expected=new Dictionary<string,string>{
            {"fk_mt4_edition_owner","course_edition.institution_id->institution.id"},
            {"fk_mt4_edition_path","course_edition.institution_id->study_path.institution_id,course_edition.study_path_id->study_path.id"},
            {"fk_mt4_ec_edition","course_edition_campus.institution_id->course_edition.institution_id,course_edition_campus.course_edition_id->course_edition.id"},
            {"fk_mt4_ec_campus","course_edition_campus.institution_id->campus.institution_id,course_edition_campus.campus_id->campus.id"},
            {"fk_mt4_enrollment_student","student_enrollment.institution_id->student.institution_id,student_enrollment.student_id->student.id"},
            {"fk_mt4_enrollment_edition","student_enrollment.institution_id->course_edition.institution_id,student_enrollment.course_edition_id->course_edition.id"},
            {"fk_mt4_class_edition","class.institution_id->course_edition.institution_id,class.course_edition_id->course_edition.id"}
        };
        Assert.Equal(expected.Count,mappings.Count);foreach(var item in expected)Assert.Equal(item.Value,mappings[item.Key]);
        cmd.CommandText="SELECT COUNT(*) FROM information_schema.referential_constraints WHERE constraint_schema=DATABASE() AND LEFT(constraint_name,7)='fk_mt4_' AND delete_rule='RESTRICT' AND update_rule='RESTRICT'";
        Assert.Equal(7,Convert.ToInt32(await cmd.ExecuteScalarAsync()));
    }
    private sealed class SqlCapture : DbCommandInterceptor
    {
        public readonly List<(string Sql,object?[] Values)> Queries=new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct=default)
        {Queries.Add((command.CommandText,command.Parameters.Cast<DbParameter>().Select(p=>p.Value).ToArray()));return ValueTask.FromResult(result);}
    }
    [Fact] public Task ReadQueriesFilterOwnerBeforeMaterialization()=>Pair(async(a,b)=>{
        var capture=new SqlCapture();var services=new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",fixture.ConnectionString}}).Build());
        services.AddDbContext<ElectronicRegisterContext>(options=>options.AddInterceptors(capture));
        await using var provider=services.BuildServiceProvider();
        using var scope=provider.CreateScope();var repo=scope.ServiceProvider.GetRequiredService<ICourseStructureRepository>();
        await repo.GetEditionsAsync(a.Tenant);await repo.GetEnrollmentsAsync(a.Tenant,b.Student);await repo.GetCampusIdsAsync(a.Tenant,b.Edition);
        Assert.Equal(3,capture.Queries.Count);
        Assert.All(capture.Queries,q=>{Assert.Contains("WHERE",q.Sql);Assert.Contains("institution_id",q.Sql);Assert.Contains(q.Values,v=>v is Guid g&&g==a.Owner);});
    });
    [Fact] public Task ClassCannotUseDifferentPathWithinSameTenant()=>Pair(async(a,b)=>{
        var path=Guid.NewGuid();var edition=Guid.NewGuid();
        await Sql("INSERT INTO study_path(id,institution_id,name) VALUES(@id,@o,'Other')",("@id",path),("@o",a.Owner));
        await Repo(r=>r.AddEditionAsync(a.Tenant,Edition(a,edition) with {Code="OTHER",StudyPathId=path}));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Repo(r=>r.AssignClassAsync(a.Tenant,a.Class,edition)));
        await Rejected("UPDATE class SET course_edition_id=@e WHERE id=@id",("@e",edition),("@id",a.Class));
    });
    [Fact] public Task RepositoryValidatesDatesAndStatusesWithoutWriting()=>Pair(async(a,b)=>{
        await Assert.ThrowsAsync<ArgumentException>(()=>Repo(r=>r.AddEditionAsync(a.Tenant,Edition(a,Guid.NewGuid()) with {Status="unknown"})));
        await Assert.ThrowsAsync<ArgumentException>(()=>Repo(r=>r.AddEditionAsync(a.Tenant,Edition(a,Guid.NewGuid()) with {StartDate=new DateOnly(2027,1,1),EndDate=new DateOnly(2026,1,1)})));
        await Assert.ThrowsAsync<ArgumentException>(()=>Repo(r=>r.AddEnrollmentAsync(a.Tenant,Enrollment(a) with {CompletionDate=new DateOnly(2020,1,1)})));
        await Repo(async r=>{Assert.Single(await r.GetEditionsAsync(a.Tenant));Assert.Empty(await r.GetEnrollmentsAsync(a.Tenant,a.Student));});
    });

    [Fact] public Task ExistingBackfillIsIdempotentWithAttributedMt4Graph()=>Pair(async(a,b)=>{
        var enrollment=Enrollment(a);
        await Repo(async r=>{await r.AddEnrollmentAsync(a.Tenant,enrollment);await r.LinkCampusAsync(a.Tenant,a.Edition,a.Campus);await r.AssignClassAsync(a.Tenant,a.Class,a.Edition);});
        var holding=Guid.Parse("4d543341-0000-4000-8000-000000000001");
        await ElectronicRegisterAPI.Infrastructure.Persistence.Migrations.Mt3aOwnershipMigration.BackfillAsync(fixture.ConnectionString,holding);
        await ElectronicRegisterAPI.Infrastructure.Persistence.Migrations.Mt3aOwnershipMigration.BackfillAsync(fixture.ConnectionString,holding);
        await Repo(async r=>{Assert.Equal(enrollment,Assert.Single(await r.GetEnrollmentsAsync(a.Tenant,a.Student)));Assert.Equal(a.Campus,Assert.Single(await r.GetCampusIdsAsync(a.Tenant,a.Edition)));});
        using var scope=fixture.ScopeFactory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal(a.Edition,await db.Classes.Where(x=>x.Id==a.Class).Select(x=>EF.Property<Guid?>(x,"CourseEditionId")).SingleAsync());
        Assert.Equal(a.Class,(await db.Students.AsNoTracking().SingleAsync(x=>x.Id==a.Student)).ClassId);
    });

    [Fact] public async Task LegacyEfWritersStillReceiveTriggerDerivedOwners()
    {
        var pathId=Guid.NewGuid();var studentId=Guid.NewGuid();var holding=Guid.Parse("4d543341-0000-4000-8000-000000000001");
        try
        {
            using(var scope=fixture.ScopeFactory.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
                db.StudyPaths.Add(new(){Id=pathId,Name="MT4 legacy EF "+pathId.ToString("N"),Description="Temporary compatibility probe"});
                db.Students.Add(new(){Id=studentId,FirstName="MT4",LastName="Legacy EF"});
                await db.SaveChangesAsync();
            }
            using(var scope=fixture.ScopeFactory.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
                Assert.Equal(holding,await db.StudyPaths.Where(x=>x.Id==pathId).Select(x=>EF.Property<Guid>(x,"InstitutionId")).SingleAsync());
                Assert.Equal(holding,await db.Students.Where(x=>x.Id==studentId).Select(x=>EF.Property<Guid>(x,"InstitutionId")).SingleAsync());
            }
        }
        finally
        {
            await Sql("DELETE FROM student WHERE id=@id",("@id",studentId));
            await Sql("DELETE FROM study_path WHERE id=@id",("@id",pathId));
        }
    }

}
