using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    public async Task AcademicFaultAfterAuditRollsBackBothResourceAndLedger(string operation)
    {
        var token = await Access();
        var id = await AcademicCreate("study-areas", AcademicInput("study-areas"));
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, configureServices: services =>
        {
            var descriptor = services.Last(s => s.ServiceType == typeof(IInstitutionAcademicCatalogRepository));
            services.AddScoped<IInstitutionAcademicCatalogRepository>(sp => new AcademicFault((IInstitutionAcademicCatalogRepository)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!), operation + "StudyAreaAsync"));
        });
        var method = operation == "Create" ? "POST" : operation == "Update" ? "PUT" : "DELETE";
        await Expect(await Request(Academic("study-areas") + (method == "POST" ? "" : "/" + id), token, method == "DELETE" ? null : AcademicInput("study-areas", "Rollback"), method, host: host, etag: "\"1\""), HttpStatusCode.InternalServerError);
        await Db(async db =>
        {
            Assert.False(await db.StudyAreas.AnyAsync(r => EF.Property<Guid>(r, "InstitutionId") == _a && r.Name == "Rollback"));
            Assert.Equal(1, await db.Set<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.InstitutionAdministrationEvent>().CountAsync(e => e.InstitutionId == _a));
            Assert.True(await db.StudyAreas.AnyAsync(r => r.Id == id));
        });
    }

    private sealed class AcademicFault(IInstitutionAcademicCatalogRepository inner, string fail) : IInstitutionAcademicCatalogRepository
    {
        public async Task<InstitutionAcademicPage<InstitutionStudyAreaDto>> ListStudyAreaAsync(TenantContext tenant, int page, int size, CancellationToken ct)
        {
            var result = await inner.ListStudyAreaAsync(tenant, page, size, ct);
            if (fail == nameof(ListStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<InstitutionStudyAreaDto?> GetStudyAreaAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            var result = await inner.GetStudyAreaAsync(tenant, id, ct);
            if (fail == nameof(GetStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<Guid> CreateStudyAreaAsync(TenantContext tenant, CreateInstitutionStudyAreaDto dto, CancellationToken ct)
        {
            var result = await inner.CreateStudyAreaAsync(tenant, dto, ct);
            if (fail == nameof(CreateStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task UpdateStudyAreaAsync(TenantContext tenant, Guid id, UpdateInstitutionStudyAreaDto dto, long revision, CancellationToken ct)
        {
            await inner.UpdateStudyAreaAsync(tenant, id, dto, revision, ct);
            if (fail == nameof(UpdateStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
        }

        public async Task DeleteStudyAreaAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            await inner.DeleteStudyAreaAsync(tenant, id, ct);
            if (fail == nameof(DeleteStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
        }

        public async Task<InstitutionAcademicPage<InstitutionStudyPathDto>> ListStudyPathAsync(TenantContext tenant, int page, int size, CancellationToken ct)
        {
            var result = await inner.ListStudyPathAsync(tenant, page, size, ct);
            if (fail == nameof(ListStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<InstitutionStudyPathDto?> GetStudyPathAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            var result = await inner.GetStudyPathAsync(tenant, id, ct);
            if (fail == nameof(GetStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<Guid> CreateStudyPathAsync(TenantContext tenant, CreateInstitutionStudyPathDto dto, CancellationToken ct)
        {
            var result = await inner.CreateStudyPathAsync(tenant, dto, ct);
            if (fail == nameof(CreateStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task UpdateStudyPathAsync(TenantContext tenant, Guid id, UpdateInstitutionStudyPathDto dto, long revision, CancellationToken ct)
        {
            await inner.UpdateStudyPathAsync(tenant, id, dto, revision, ct);
            if (fail == nameof(UpdateStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
        }

        public async Task DeleteStudyPathAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            await inner.DeleteStudyPathAsync(tenant, id, ct);
            if (fail == nameof(DeleteStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
        }

        public async Task<InstitutionAcademicPage<InstitutionBienniumDto>> ListBienniumAsync(TenantContext tenant, int page, int size, CancellationToken ct)
        {
            var result = await inner.ListBienniumAsync(tenant, page, size, ct);
            if (fail == nameof(ListBienniumAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<InstitutionBienniumDto?> GetBienniumAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            var result = await inner.GetBienniumAsync(tenant, id, ct);
            if (fail == nameof(GetBienniumAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<Guid> CreateBienniumAsync(TenantContext tenant, CreateInstitutionBienniumDto dto, CancellationToken ct)
        {
            var result = await inner.CreateBienniumAsync(tenant, dto, ct);
            if (fail == nameof(CreateBienniumAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task UpdateBienniumAsync(TenantContext tenant, Guid id, UpdateInstitutionBienniumDto dto, long revision, CancellationToken ct)
        {
            await inner.UpdateBienniumAsync(tenant, id, dto, revision, ct);
            if (fail == nameof(UpdateBienniumAsync))
                throw new InvalidOperationException("Injected after administrative audit");
        }

        public async Task DeleteBienniumAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            await inner.DeleteBienniumAsync(tenant, id, ct);
            if (fail == nameof(DeleteBienniumAsync))
                throw new InvalidOperationException("Injected after administrative audit");
        }

        public async Task<InstitutionAcademicPage<InstitutionBienniumStudyAreaDto>> ListBienniumStudyAreaAsync(TenantContext tenant, int page, int size, CancellationToken ct)
        {
            var result = await inner.ListBienniumStudyAreaAsync(tenant, page, size, ct);
            if (fail == nameof(ListBienniumStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<InstitutionBienniumStudyAreaDto?> GetBienniumStudyAreaAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            var result = await inner.GetBienniumStudyAreaAsync(tenant, id, ct);
            if (fail == nameof(GetBienniumStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<Guid> CreateBienniumStudyAreaAsync(TenantContext tenant, CreateInstitutionBienniumStudyAreaDto dto, CancellationToken ct)
        {
            var result = await inner.CreateBienniumStudyAreaAsync(tenant, dto, ct);
            if (fail == nameof(CreateBienniumStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task DeleteBienniumStudyAreaAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            await inner.DeleteBienniumStudyAreaAsync(tenant, id, ct);
            if (fail == nameof(DeleteBienniumStudyAreaAsync))
                throw new InvalidOperationException("Injected after administrative audit");
        }

        public async Task<InstitutionAcademicPage<InstitutionBienniumStudyPathDto>> ListBienniumStudyPathAsync(TenantContext tenant, int page, int size, CancellationToken ct)
        {
            var result = await inner.ListBienniumStudyPathAsync(tenant, page, size, ct);
            if (fail == nameof(ListBienniumStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<InstitutionBienniumStudyPathDto?> GetBienniumStudyPathAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            var result = await inner.GetBienniumStudyPathAsync(tenant, id, ct);
            if (fail == nameof(GetBienniumStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task<Guid> CreateBienniumStudyPathAsync(TenantContext tenant, CreateInstitutionBienniumStudyPathDto dto, CancellationToken ct)
        {
            var result = await inner.CreateBienniumStudyPathAsync(tenant, dto, ct);
            if (fail == nameof(CreateBienniumStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
            return result;
        }

        public async Task DeleteBienniumStudyPathAsync(TenantContext tenant, Guid id, CancellationToken ct)
        {
            await inner.DeleteBienniumStudyPathAsync(tenant, id, ct);
            if (fail == nameof(DeleteBienniumStudyPathAsync))
                throw new InvalidOperationException("Injected after administrative audit");
        }
    }
}
