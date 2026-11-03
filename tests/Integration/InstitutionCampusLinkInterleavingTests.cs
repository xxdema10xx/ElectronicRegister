using System;
using System.Data.Common;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    // Pause only after MySQL has executed the real INSERT, retaining all its locks.
    // No repository, transaction, HTTP result or provider exception is substituted.
    private sealed class PauseInsertedCampusLink : DbCommandInterceptor
    {
        internal TaskCompletionSource<bool> Inserted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private async Task Pause(DbCommand command, CancellationToken ct)
        {
            if (command.CommandText.Contains("INSERT INTO `course_edition_campus`", StringComparison.OrdinalIgnoreCase))
            {
                Inserted.TrySetResult(true);
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data, DbDataReader result, CancellationToken ct = default)
        {
            await Pause(command, ct);
            return result;
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data, int result, CancellationToken ct = default)
        {
            await Pause(command, ct);
            return result;
        }
    }

    [Fact]
    public async Task CampusDeleteOverlappingAnInsertedLinkCannotLoseTheLinkOrReturn500()
    {
        var campus = await CreateCampus();
        var edition = await Edition();
        var token = await Access();
        var pause = new PauseInsertedCampusLink();
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, [], enabled: false, school: true, course: true,
            configureServices: services =>
            {
                services.AddDbContext<ElectronicRegisterContext>(options => options.AddInterceptors(pause));
                services.Insert(0, ServiceDescriptor.Singleton<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>(_campusFailure));
            });
        var post = SqlLockEvidence.Track(() => Request($"/api/institution/course-editions/{edition}/campuses", token, new { CampusId = campus }, "POST", host));
        await pause.Inserted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var delete = SqlLockEvidence.Track(() => Request(Campuses + "/" + campus, token, method: "DELETE"));
        try { await SqlLockEvidence.WaitAsync(fixture.ConnectionString, delete, post); }
        finally { pause.Release.TrySetResult(true); }
        var responses = await Task.WhenAll(post, delete);
        Assert.True(responses[0].StatusCode == HttpStatusCode.Created, $"POST {responses[0].StatusCode}: {_campusFailure.Failure}");
        await Expect(responses[1], HttpStatusCode.Conflict);
        await Db(async db =>
        {
            Assert.True(await db.Campuses.AnyAsync(c => c.Id == campus && c.InstitutionId == _a));
            Assert.True(await db.CourseEditions.AnyAsync(e => e.Id == edition && e.InstitutionId == _a));
            Assert.True(await db.CourseEditionCampuses.AnyAsync(c => c.InstitutionId == _a && c.CourseEditionId == edition && c.CampusId == campus));
            Assert.Equal(1, await db.CourseAdministrationEvents.CountAsync(e => e.InstitutionId == _a && e.CourseEditionId == edition && e.CampusId == campus && e.Action == "campus.link"));
        });
    }
}
