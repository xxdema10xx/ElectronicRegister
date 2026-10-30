using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Fact]
    public async Task Quality_AutosaveDisposesOnlyItsOwnedTransaction()
    {
        var prepared = await PrepareQualityWrite("student", "answers");
        var id = Guid.Parse(prepared.Route.Split('/')[4]);
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ElectronicRegisterAPI.Domain.Interfaces.Repositories.IExamSubmissionRepository>();
        Assert.True((await repository.SaveAnswersAsync(id, 1, [])).Accepted);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.False((await repository.SaveAnswersAsync(id, 1, [])).Accepted);
        Assert.Null(db.Database.CurrentTransaction);
        await using (var caller = await db.Database.BeginTransactionAsync())
        {
            Assert.True((await repository.SaveAnswersAsync(id, 2, [])).Accepted);
            Assert.Same(caller, db.Database.CurrentTransaction);
            await caller.RollbackAsync();
        }
        Assert.Equal((ulong)1, (await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == id)).LastClientRevision);
    }
    [Fact]
    public async Task Quality_GradePaginationHasTotalOrderAndRejectsOverflow()
    {
        var token = await Access();
        for (var n = 0; n < 4; n++)
            await Expect(await Request("/api/institution/grades", token, new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 7, Date = new DateOnly(2026,10,27) }, "POST"), HttpStatusCode.Created);
        var pages = new System.Collections.Generic.List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var result = (await (await Request("/api/institution/grades/paged?pageNumber=" + page + "&pageSize=1", token)).Content.ReadFromJsonAsync<GradePageDto>())!;
            pages.Add(Assert.Single(result.Items).Id);
        }
        Assert.Equal(4, pages.Distinct().Count());
        await Db(async db => Assert.Equal(await db.Grades.Where(g => EF.Property<Guid>(g, "InstitutionId") == _a).OrderBy(g => g.Id).Select(g => g.Id).ToListAsync(), pages));
        await Expect(await Request("/api/institution/grades/paged?pageNumber=2147483647&pageSize=100", token), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Quality_CancellationBeforeRepositoryCreationNeverWrites()
    {
        using var scope = _host.Host.Services.CreateScope();
        var token = new CancellationToken(true);
        // Cancellation is checked before a factory can create a bound scope.
        var factory = scope.ServiceProvider.GetRequiredService<IInstitutionExamRepositoryFactory>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await scope.ServiceProvider.GetRequiredService<IInstitutionExamManager>().ExamsAsync(token));
    }

    [Fact]
    public async Task Quality_UnknownEntityCannotUseTenantOnlyExamFallback()
    {
        // The scope is internal by design; this is a boundary regression, not a new API.
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        var context = await PersistedTestTenant.ReadAsync(db, new(_adminUser, _a, Id("admin-membership"), [ElectronicRegisterAPI.Domain.Enums.InstitutionRole.InstitutionAdmin]));
        var type = typeof(ElectronicRegisterContext).Assembly.GetType("ElectronicRegisterAPI.Infrastructure.Repositories.InstitutionExamDataScope", true)!;
        var bound = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
            [db, new ElectronicRegisterAPI.Domain.Models.ExamPrincipal(context, false, null, null), scope.ServiceProvider.GetRequiredService<IInstitutionWriteAuthorization>(), CancellationToken.None], null)!;
        var method = type.GetMethod("Query", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.MakeGenericMethod(typeof(ElectronicRegisterAPI.Infrastructure.Persistence.Entities.User));
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(bound, null));
        Assert.IsType<UnauthorizedAccessException>(error.InnerException);
    }

    [Fact]
    public async Task Quality_ReadinessRequiresReleaseMarkerAndExecutePermission()
    {
        await Expect(await Request("/health/ready"), HttpStatusCode.OK);
        await Execute("DELETE FROM er_schema_release WHERE version='er-quality-1'");
        try
        {
            await Expect(await Request("/health/ready"), HttpStatusCode.ServiceUnavailable);
            await Expect(await Request("/health/live"), HttpStatusCode.OK);
        }
        finally { await Execute("INSERT INTO er_schema_release(version) VALUES('er-quality-1')"); }
        await Expect(await Request("/health/ready"), HttpStatusCode.OK);
        var user = "qready_" + Guid.NewGuid().ToString("N")[..12];
        var secret = Guid.NewGuid().ToString("N");
        await Execute("CREATE USER '" + user + "'@'localhost' IDENTIFIED BY '" + secret + "'");
        try
        {
            await Execute("GRANT SELECT ON electronic_register_test.* TO '" + user + "'@'localhost'");
            var connection = new MySqlConnectionStringBuilder(fixture.ConnectionString) { UserID = user, Password = secret };
            await using var host = await Mt3cHosting.ProgramHost.StartAsync(connection.ConnectionString, _a, [], enabled: false);
            await Expect(await Request("/health/ready", host: host), HttpStatusCode.ServiceUnavailable);
            await Expect(await Request("/health/live", host: host), HttpStatusCode.OK);
        }
        finally { await Execute("DROP USER '" + user + "'@'localhost'"); }
    }
}

[Collection(IntegrationCollection.Name)]
public sealed class QualityPasswordQuotaTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task SharedQuotaSerializesReplicasAndPersistsAcrossScopes()
    {
        var user = Guid.NewGuid();
        using (var setup = fixture.ScopeFactory.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            db.Users.Add(new() { Id = user, Email = user + "@quota.example.test", PasswordHash = "", Role = "student" });
            await db.SaveChangesAsync();
        }
        async Task<Guid?> Begin()
        {
            using var replica = fixture.ScopeFactory.CreateScope();
            return await replica.ServiceProvider.GetRequiredService<IIdentityAccountRepository>().BeginPasswordAttemptAsync(user);
        }
        var leases = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Begin()));
        var lease = Assert.Single(leases.Where(x => x.HasValue))!.Value;
        using var scope = fixture.ScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIdentityAccountRepository>();
        await repository.EndPasswordAttemptAsync(user, lease);
        for (var n = 0; n < 4; n++)
        {
            var next = await Begin();
            Assert.NotNull(next);
            await repository.EndPasswordAttemptAsync(user, next!.Value);
        }
        Assert.Null(await Begin());
        // Source IP, forwarded headers, JWT jti and application replica do not
        // participate in the quota key, so rotating them cannot reset the budget.
    }
}
