using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Theory]
    [InlineData("grades", false)][InlineData("grades", true)]
    [InlineData("exams", false)][InlineData("exams", true)]
    public async Task StaffRole_FaultAfterPersistingAuditRollsBackWholeResource(string resource, bool update)
    {
        var token = await StaffToken(); var route = "/api/institution/" + resource;
        object create = resource == "grades" ? new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 7, Date = new DateOnly(2026,10,27) } : StaffExamInput();
        Guid? id = null;
        if (update)
        {
            var response = await Request(route, token, create, "POST"); await Expect(response, HttpStatusCode.Created);
            id = await response.Content.ReadFromJsonAsync<Guid>();
        }
        object body = create;
        if (update) body = resource == "grades" ? new UpdateGradeDto { SubjectId = Id("subject"), Value = 9, Date = new DateOnly(2026,10,27) } :
            new UpdateExamDto { ClassSubjectId = Id("assignment"), Description = "Rollback", AvailableFrom = DateTime.UtcNow.AddMinutes(-5), AvailableUntil = DateTime.UtcNow.AddHours(2), DurationMinutes = 30, PassingPercent = 60, Questions = StaffExamInput().Questions };
        var before = await Digest(false);
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, exam: true,
            configureServices: services =>
            {
                var descriptor = services.Last(s => s.ServiceType == typeof(IInstitutionExamRepositoryFactory));
                services.AddScoped<IInstitutionExamRepositoryFactory>(sp => new StaffAuditFaultFactory((IInstitutionExamRepositoryFactory)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!)));
            });
        await Expect(await Request(route + (id.HasValue ? "/" + id : ""), token, body, update ? "PUT" : "POST", host), HttpStatusCode.InternalServerError);
        Assert.Equal(before, await Digest(false));
    }

    private sealed class StaffAuditFaultFactory(IInstitutionExamRepositoryFactory inner) : IInstitutionExamRepositoryFactory
    {
        public IInstitutionExamRepositories Create(ExamPrincipal actor)
        {
            var proxy = DispatchProxy.Create<IInstitutionExamRepositories, StaffAuditProxy>();
            ((StaffAuditProxy)(object)proxy).Inner = inner.Create(actor);
            return proxy;
        }
    }
    public class StaffAuditProxy : DispatchProxy
    {
        public IInstitutionExamRepositories Inner { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Audit"
            ? new StaffAuditFault(Inner.Audit) : method.Invoke(Inner, args);
    }
    private sealed class StaffAuditFault(IAuditRepository inner) : IAuditRepository
    {
        public async Task AddAsync(AuditEvent value) { await inner.AddAsync(value); throw new InvalidOperationException("Staff injected fault after persisted audit"); }
    }

    [Fact]
    public async Task StaffRole_RevocationWaitsForAuthorizedWriteAndThenBlocksOldToken()
    {
        var token = await StaffToken();
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false,
            school: true, exam: true, membershipAdmin: true, membershipWrite: true, configureServices: services =>
            {
                var descriptor = services.Last(s => s.ServiceType == typeof(IInstitutionWriteAuthorization));
                services.AddScoped<IInstitutionWriteAuthorization>(sp => new StaffWriteBarrier((IInstitutionWriteAuthorization)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), acquired, release));
            });
        var admin = await Access(); long revision = -1;
        await Db(async db => revision = (await db.InstitutionMembershipSecurityStates.AsNoTracking().SingleAsync(s => s.MembershipId == Id("teacher-membership"))).SecurityGeneration);
        var write = Request(Route("teacher"), token, Input("teacher", "Before revocation"), "POST", host);
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var revoke = Request("/api/institution/admin/members/" + Id("teacher-membership") + "/mutations", admin,
            new { EventId = Guid.NewGuid(), ExpectedRevision = revision, Action = "RemoveRole", Role = "Staff", Reason = "Concurrent revocation" }, "POST", host);
        try
        {
            // Keep the authorized write open while a real concurrent IAM HTTP
            // request runs; success is checked only after that transaction commits.
            await Task.WhenAny(revoke, Task.Delay(250));
            Assert.False(revoke.IsCompleted);
        }
        finally { release.TrySetResult(true); }
        await Expect(await write, HttpStatusCode.Created);
        await Expect(await revoke, HttpStatusCode.OK);
        await Expect(await Request(Route("teacher"), token, Input("teacher"), "POST"), HttpStatusCode.Forbidden);
    }
    private sealed class StaffWriteBarrier(IInstitutionWriteAuthorization inner, TaskCompletionSource<bool> acquired, TaskCompletionSource<bool> release) : IInstitutionWriteAuthorization
    {
        public async Task LockAsync(TenantContext actor, CancellationToken ct = default)
        { await inner.LockAsync(actor, ct); acquired.TrySetResult(true); await release.Task.WaitAsync(ct); }
        public void RequireLocked(TenantContext actor) => inner.RequireLocked(actor);
    }
}
