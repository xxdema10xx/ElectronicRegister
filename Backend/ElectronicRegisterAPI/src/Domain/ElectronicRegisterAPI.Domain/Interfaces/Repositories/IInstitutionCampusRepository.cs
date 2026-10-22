using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;
public interface IInstitutionCampusRepository
{
    Task<InstitutionAcademicPage<InstitutionCampusDto>> ListAsync(TenantContext t, int page, int size, bool? isActive, CancellationToken ct);
    Task<InstitutionCampusDto?> GetAsync(TenantContext t, Guid id, CancellationToken ct);
    Task<Guid> CreateAsync(TenantContext t, CreateInstitutionCampusDto dto, CancellationToken ct);
    Task UpdateAsync(TenantContext t, Guid id, UpdateInstitutionCampusDto dto, long revision, CancellationToken ct);
    Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct);
}
