using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionSubjectRepository
{
    Task<List<Subject>> GetAllAsync(TenantContext tenant, CancellationToken ct = default);
    Task<int> CountAsync(TenantContext tenant, CancellationToken ct = default);
    Task<Subject?> GetByIdAsync(TenantContext tenant, Guid id, CancellationToken ct = default);
    Task<List<Subject>> GetByIdsAsync(TenantContext tenant, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Subject?> GetByNameAsync(TenantContext tenant, string name, CancellationToken ct = default);
    Task<List<Subject>> GetByTeacherAsync(TenantContext tenant, Guid teacherId, CancellationToken ct = default);
    Task AddAsync(TenantContext tenant, Subject subject, CancellationToken ct = default);
    Task UpdateAsync(TenantContext tenant, Subject subject, CancellationToken ct = default);
    Task DeleteAsync(TenantContext tenant, Guid id, CancellationToken ct = default);
}
