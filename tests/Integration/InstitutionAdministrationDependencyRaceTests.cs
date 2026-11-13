using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    private readonly CampusFailureEvidence _campusFailure = new();
    private sealed class CampusFailureEvidence : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
    {
        internal Exception? Failure;
        public ValueTask<bool> TryHandleAsync(Microsoft.AspNetCore.Http.HttpContext context, Exception exception, System.Threading.CancellationToken ct)
        {
            Failure = exception;
            return ValueTask.FromResult(false);
        }
    }

    [Theory]
    [InlineData(1205)] [InlineData(1213)]
    public async Task CampusLinkProviderConcurrencyErrorsRollBackTheWholeAuditedOperation(int number)
    {
        var campus = await CreateCampus();
        var edition = await Edition();
        await using var blocker = new MySqlConnection(fixture.ConnectionString);
        await blocker.OpenAsync();
        await using (var identity = new MySqlCommand("SELECT VERSION(),@@transaction_isolation,@@foreign_key_checks", blocker))
        await using (var reader = await identity.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("8.4.13", reader.GetString(0));
            Assert.Equal("REPEATABLE-READ", reader.GetString(1));
            Assert.Equal(1, reader.GetInt32(2));
        }
        // Give the blocking transaction greater rollback weight so InnoDB deterministically
        // selects the application INSERT as the deadlock victim. This table is session local.
        await using (var setup = new MySqlCommand("CREATE TEMPORARY TABLE campus_race_weight (id INT PRIMARY KEY, value INT NOT NULL) ENGINE=InnoDB; INSERT INTO campus_race_weight WITH RECURSIVE n AS (SELECT 1 AS id UNION ALL SELECT id+1 FROM n WHERE id<100) SELECT id,0 FROM n", blocker))
            await setup.ExecuteNonQueryAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        async Task Lock(string sql, Guid id, bool expectRow = true)
        {
            await using var command = new MySqlCommand(sql, blocker, tx);
            command.Parameters.AddWithValue("@t", _a);
            command.Parameters.AddWithValue("@id", id);
            var row = await command.ExecuteScalarAsync();
            if (expectRow) Assert.NotNull(row);
            else Assert.Null(row);
        }
        await using (var weight = new MySqlCommand("UPDATE campus_race_weight SET value=value+1", blocker, tx))
            await weight.ExecuteNonQueryAsync();
        if (number == 1213)
        {
            // Reproduce the DELETE dependency scan's empty audit range before it
            // reaches the link row: the POST can insert the link, then waits on audit.
            await using var gap = new MySqlCommand("SELECT id FROM course_administration_event WHERE institution_id=@t AND campus_id=@id LIMIT 1 FOR UPDATE", blocker, tx);
            gap.Parameters.AddWithValue("@t", _a);
            gap.Parameters.AddWithValue("@id", campus);
            Assert.Null(await gap.ExecuteScalarAsync());
        }
        else
            await Lock("SELECT id FROM course_edition FORCE INDEX (uq_mt4_edition_owner) WHERE institution_id=@t AND id=@id FOR UPDATE", edition);
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync(number == 1213 ? "SET SESSION innodb_lock_wait_timeout=30" : "SET SESSION innodb_lock_wait_timeout=3");
        var repository = scope.ServiceProvider.GetRequiredService<ICourseStructureRepository>();
        var actor = new TenantContext(_adminUser, _a, Id("admin-membership"), [InstitutionRole.InstitutionAdmin]);
        var write = SqlLockEvidence.Track((MySqlConnection)db.Database.GetDbConnection(), async () =>
        {
            await repository.LinkCampusAuditedAsync(actor, edition, campus, default);
            return true;
        });
        try
        {
            try { await SqlLockEvidence.WaitAsync(fixture.ConnectionString, write, blocker); }
            catch
            {
                if (write.IsCompleted) await write;
                throw;
            }
            if (number == 1213)
                await Lock("SELECT campus_id FROM course_edition_campus WHERE institution_id=@t AND campus_id=@id LIMIT 1 FOR UPDATE", campus, expectRow: false);
            var failure = await Record.ExceptionAsync(() => write);
            if (number == 1213)
            {
                await using var status = new MySqlCommand("SHOW ENGINE INNODB STATUS", blocker, tx);
                await using var reader = await status.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                var graph = reader.GetString(2);
                Assert.Contains("LATEST DETECTED DEADLOCK", graph);
                Assert.Contains("course_administration_event", graph);
                Assert.Contains("course_edition_campus", graph);
                Console.WriteLine(graph);
            }
            Assert.True(failure is CourseConflictException, failure?.ToString() ?? "The blocked operation unexpectedly committed");
            var error = (CourseConflictException)failure!;
            if (number == 1213) Assert.IsType<MySqlException>(error.InnerException);
            else Assert.IsType<InvalidOperationException>(error.InnerException);
            Exception? cause = error;
            while (cause is not null && cause is not MySqlException) cause = cause.InnerException;
            Assert.Equal(number, Assert.IsType<MySqlException>(cause).Number);
        }
        finally { await tx.RollbackAsync(); }
        await Db(async verify =>
        {
            Assert.True(await verify.Campuses.AnyAsync(c => c.Id == campus && c.InstitutionId == _a));
            Assert.True(await verify.CourseEditions.AnyAsync(e => e.Id == edition && e.InstitutionId == _a));
            Assert.False(await verify.CourseEditionCampuses.AnyAsync(c => c.InstitutionId == _a && c.CampusId == campus));
            Assert.False(await verify.CourseAdministrationEvents.AnyAsync(e => e.InstitutionId == _a && e.CampusId == campus));
        });
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task CampusLinkAndDeletionInEitherCommittedOrderPreserveIntegrity(bool linkFirst)
    {
        var campus = await CreateCampus();
        var edition = await Edition();
        var token = await Access();
        if (linkFirst)
        {
            await Expect(await Request($"/api/institution/course-editions/{edition}/campuses", token, new { CampusId = campus }, "POST"), HttpStatusCode.Created);
            await Expect(await Request(Campuses + "/" + campus, token, method: "DELETE"), HttpStatusCode.Conflict);
        }
        else
        {
            await Expect(await Request(Campuses + "/" + campus, token, method: "DELETE"), HttpStatusCode.NoContent);
            await Expect(await Request($"/api/institution/course-editions/{edition}/campuses", token, new { CampusId = campus }, "POST"), HttpStatusCode.NotFound);
        }
        await Db(async db =>
        {
            Assert.Equal(linkFirst, await db.Campuses.AnyAsync(c => c.Id == campus));
            Assert.Equal(linkFirst, await db.CourseEditionCampuses.AnyAsync(c => c.InstitutionId == _a && c.CampusId == campus && c.CourseEditionId == edition));
            Assert.True(await db.CourseEditions.AnyAsync(e => e.Id == edition && e.InstitutionId == _a));
        });
    }

    [Fact]
    public async Task CampusDeletionAndLinkActuallyWaitOnTheSameParentLock()
    {
        var campus = await CreateCampus();
        var edition = await Edition();
        var token = await Access();
        await using var blocker = new MySqlConnection(fixture.ConnectionString);
        await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await using (var command = new MySqlCommand("SELECT id FROM campus FORCE INDEX (uq_mt4_campus_owner) WHERE institution_id=@t AND id=@c FOR UPDATE", blocker, tx))
        {
            command.Parameters.AddWithValue("@t", _a);
            command.Parameters.AddWithValue("@c", campus);
            Assert.NotNull(await command.ExecuteScalarAsync());
        }
        var delete = SqlLockEvidence.Track(() => Request(Campuses + "/" + campus, token, method: "DELETE"));
        await SqlLockEvidence.WaitAsync(fixture.ConnectionString, delete, blocker);
        var post = SqlLockEvidence.Track(() => Request($"/api/institution/course-editions/{edition}/campuses", token, new { CampusId = campus }, "POST"));
        try { await SqlLockEvidence.WaitAsync(fixture.ConnectionString, post, blocker); }
        catch
        {
            var response = post.IsCompletedSuccessfully ? await post : null;
            Assert.Fail(_campusFailure.Failure?.ToString() ?? (response is not null ? $"POST {response.StatusCode}: {await response.Content.ReadAsStringAsync()}" : "POST has no captured failure"));
        }
        finally { await tx.CommitAsync(); }
        var responses = await Task.WhenAll(post, delete);
        var linked = responses[0].StatusCode == HttpStatusCode.Created;
        await Expect(responses[1], linked ? HttpStatusCode.Conflict : HttpStatusCode.NoContent);
        Assert.True(linked || responses[0].StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict,
            $"POST {responses[0].StatusCode}: {_campusFailure.Failure}");
        await Db(async db =>
        {
            Assert.Equal(linked, await db.Campuses.AnyAsync(c => c.Id == campus));
            Assert.Equal(linked, await db.CourseEditionCampuses.AnyAsync(c => c.InstitutionId == _a && c.CampusId == campus));
            Assert.True(await db.CourseEditions.AnyAsync(e => e.Id == edition && e.InstitutionId == _a));
        });
    }

    public static IEnumerable<object[]> CampusRaceRepetitions => Enumerable.Range(0, 100).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(CampusRaceRepetitions))]
    public async Task CampusDeletionAndCourseLinkPreserveEditionAndEveryCommittedLink(int repetition)
    {
        var id = await CreateCampus();
        var edition = await Edition();
        var token = await Access();
        var results = await Task.WhenAll(Request($"/api/institution/course-editions/{edition}/campuses", token, new { CampusId = id }, "POST"), Request(Campuses + "/" + id, token, method: "DELETE"));
        if (results[0].StatusCode == HttpStatusCode.Created)
        {
            await Expect(results[1], HttpStatusCode.Conflict);
            await Db(async db =>
            {
                Assert.True(await db.Campuses.AnyAsync(c => c.Id == id));
                Assert.True(await db.CourseEditionCampuses.AnyAsync(c => c.InstitutionId == _a && c.CampusId == id));
            });
        }
        else
        {
            Assert.True(results[0].StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict,
                $"Repetition {repetition}: POST {results[0].StatusCode}: {_campusFailure.Failure ?? (object)await results[0].Content.ReadAsStringAsync()}");
            await Expect(results[1], HttpStatusCode.NoContent);
            await Db(async db => Assert.False(await db.CourseEditionCampuses.AnyAsync(c => c.InstitutionId == _a && c.CampusId == id)));
        }

        await Db(async db => Assert.True(await db.CourseEditions.AnyAsync(e => e.Id == edition && e.InstitutionId == _a)));
    }

    [Fact]
    public async Task AcademicPathDeletionAndClassCreationNeverCascadeACommittedClass()
    {
        var bsa = await AcademicCreate("biennium-study-areas", new { BienniumId = Id("biennium"), StudyAreaId = await AcademicCreate("study-areas", AcademicInput("study-areas")) });
        var path = await AcademicCreate("biennium-study-paths", new { BienniumStudyAreaId = bsa, StudyPathId = Id("path") });
        var token = await Access();
        var results = await Task.WhenAll(Request(Classes, token, new { Name = "Concurrent", BienniumStudyPathId = path }, "POST"), Request(Academic("biennium-study-paths") + "/" + path, token, method: "DELETE"));
        if (results[0].StatusCode == HttpStatusCode.Created)
        {
            await Expect(results[1], HttpStatusCode.Conflict);
            await Db(async db => Assert.True(await db.Classes.AnyAsync(c => c.BienniumStudyPathId == path)));
        }
        else
        {
            Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Conflict });
            await Expect(results[1], HttpStatusCode.NoContent);
            await Db(async db => Assert.False(await db.Classes.AnyAsync(c => c.BienniumStudyPathId == path)));
        }
    }
}
