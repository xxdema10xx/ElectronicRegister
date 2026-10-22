using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    public async Task CampusFailureAfterAuditRollsBackEveryMutation(string operation)
    {
        var id = await CreateCampus();
        var token = await Access();
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, course: true, configureServices: services =>
        {
            var d = services.Last(s => s.ServiceType == typeof(IInstitutionCampusRepository));
            services.AddScoped<IInstitutionCampusRepository>(sp => new CampusFault((IInstitutionCampusRepository)ActivatorUtilities.CreateInstance(sp, d.ImplementationType!), operation));
        });
        var method = operation == "Create" ? "POST" : operation == "Update" ? "PUT" : "DELETE";
        object? body = method == "POST" ? new
        {
            Name = "Rollback"
        }

        : method == "PUT" ? CampusUpdate("Rollback") : null;
        await Expect(await Request(Campuses + (method == "POST" ? "" : "/" + id), token, body, method, host: host, etag: "\"1\""), HttpStatusCode.InternalServerError);
        await Db(async db =>
        {
            Assert.Equal("Sede nuova", (await db.Campuses.SingleAsync(c => c.Id == id)).Name);
            Assert.Equal(1, await db.Campuses.CountAsync(c => c.InstitutionId == _a));
            Assert.Equal(1, await db.Set<InstitutionAdministrationEvent>().CountAsync(e => e.InstitutionId == _a));
        });
    }

    [Fact]
    public async Task CampusRevocationAfterManagerAuthorizationIsRevalidatedInsideTransaction()
    {
        var token = await Access();
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, course: true, configureServices: services =>
        {
            var d = services.Last(s => s.ServiceType == typeof(IInstitutionCampusRepository));
            services.AddScoped<IInstitutionCampusRepository>(sp => new CampusFault((IInstitutionCampusRepository)ActivatorUtilities.CreateInstance(sp, d.ImplementationType!), "", () => Execute("UPDATE institution_membership SET status='suspended' WHERE institution_id=@i AND id=@m", ("@i", _a), ("@m", Id("admin-membership")))));
        });
        await Expect(await Request(Campuses, token, new { Name = "Must not persist" }, "POST", host: host), HttpStatusCode.Forbidden);
        await Db(async db =>
        {
            Assert.False(await db.Campuses.AnyAsync(c => c.InstitutionId == _a));
            Assert.False(await db.Set<InstitutionAdministrationEvent>().AnyAsync(e => e.InstitutionId == _a));
        });
    }

    private sealed class CampusFault(IInstitutionCampusRepository inner, string fail, Func<Task>? beforeCreate = null) : IInstitutionCampusRepository
    {
        public Task<InstitutionAcademicPage<InstitutionCampusDto>> ListAsync(TenantContext t, int page, int size, bool? active, CancellationToken ct) => inner.ListAsync(t, page, size, active, ct);
        public Task<InstitutionCampusDto?> GetAsync(TenantContext t, Guid id, CancellationToken ct) => inner.GetAsync(t, id, ct);
        public async Task<Guid> CreateAsync(TenantContext t, CreateInstitutionCampusDto dto, CancellationToken ct)
        {
            if (beforeCreate is not null)
                await beforeCreate();
            var id = await inner.CreateAsync(t, dto, ct);
            if (fail == "Create")
                throw new InvalidOperationException("Injected after audit");
            return id;
        }

        public async Task UpdateAsync(TenantContext t, Guid id, UpdateInstitutionCampusDto dto, long revision, CancellationToken ct)
        {
            await inner.UpdateAsync(t, id, dto, revision, ct);
            if (fail == "Update")
                throw new InvalidOperationException("Injected after audit");
        }

        public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct)
        {
            await inner.DeleteAsync(t, id, ct);
            if (fail == "Delete")
                throw new InvalidOperationException("Injected after audit");
        }
    }
}
