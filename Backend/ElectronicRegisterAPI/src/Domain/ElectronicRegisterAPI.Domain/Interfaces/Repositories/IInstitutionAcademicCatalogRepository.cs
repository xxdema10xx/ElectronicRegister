using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;
public interface IInstitutionAcademicCatalogRepository
{
    Task<InstitutionAcademicPage<InstitutionStudyAreaDto>> ListStudyAreaAsync(TenantContext tenant, int page, int size, CancellationToken ct);
    Task<InstitutionStudyAreaDto?> GetStudyAreaAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<Guid> CreateStudyAreaAsync(TenantContext tenant, CreateInstitutionStudyAreaDto dto, CancellationToken ct);
    Task UpdateStudyAreaAsync(TenantContext tenant, Guid id, UpdateInstitutionStudyAreaDto dto, long revision, CancellationToken ct);
    Task DeleteStudyAreaAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<InstitutionAcademicPage<InstitutionStudyPathDto>> ListStudyPathAsync(TenantContext tenant, int page, int size, CancellationToken ct);
    Task<InstitutionStudyPathDto?> GetStudyPathAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<Guid> CreateStudyPathAsync(TenantContext tenant, CreateInstitutionStudyPathDto dto, CancellationToken ct);
    Task UpdateStudyPathAsync(TenantContext tenant, Guid id, UpdateInstitutionStudyPathDto dto, long revision, CancellationToken ct);
    Task DeleteStudyPathAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<InstitutionAcademicPage<InstitutionBienniumDto>> ListBienniumAsync(TenantContext tenant, int page, int size, CancellationToken ct);
    Task<InstitutionBienniumDto?> GetBienniumAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<Guid> CreateBienniumAsync(TenantContext tenant, CreateInstitutionBienniumDto dto, CancellationToken ct);
    Task UpdateBienniumAsync(TenantContext tenant, Guid id, UpdateInstitutionBienniumDto dto, long revision, CancellationToken ct);
    Task DeleteBienniumAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<InstitutionAcademicPage<InstitutionBienniumStudyAreaDto>> ListBienniumStudyAreaAsync(TenantContext tenant, int page, int size, CancellationToken ct);
    Task<InstitutionBienniumStudyAreaDto?> GetBienniumStudyAreaAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<Guid> CreateBienniumStudyAreaAsync(TenantContext tenant, CreateInstitutionBienniumStudyAreaDto dto, CancellationToken ct);
    Task DeleteBienniumStudyAreaAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<InstitutionAcademicPage<InstitutionBienniumStudyPathDto>> ListBienniumStudyPathAsync(TenantContext tenant, int page, int size, CancellationToken ct);
    Task<InstitutionBienniumStudyPathDto?> GetBienniumStudyPathAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<Guid> CreateBienniumStudyPathAsync(TenantContext tenant, CreateInstitutionBienniumStudyPathDto dto, CancellationToken ct);
    Task DeleteBienniumStudyPathAsync(TenantContext tenant, Guid id, CancellationToken ct);
}
