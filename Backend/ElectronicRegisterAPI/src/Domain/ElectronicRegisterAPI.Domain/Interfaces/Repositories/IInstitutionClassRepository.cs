using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionClassRepository
{
    Task<InstitutionClassPageDto> GetAllAsync(TenantContext tenant, int page, int size, Guid? path, Guid? edition, CancellationToken ct);
    Task<InstitutionClassDto?> GetByIdAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task AddAsync(TenantContext tenant, Guid id, string name, Guid path, CancellationToken ct);
    Task UpdateAsync(TenantContext tenant, Guid id, string name, long expectedRevision, CancellationToken ct);
    Task DeleteAsync(TenantContext tenant, Guid id, CancellationToken ct);
}
