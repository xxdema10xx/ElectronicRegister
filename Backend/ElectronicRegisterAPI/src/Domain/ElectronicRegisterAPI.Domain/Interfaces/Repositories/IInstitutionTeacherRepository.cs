using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionTeacherRepository
{
    Task<List<Teacher>> GetAllAsync(TenantContext tenant, CancellationToken ct = default);
    Task<int> CountAsync(TenantContext tenant, CancellationToken ct = default);
    Task<Teacher?> GetByIdAsync(TenantContext tenant, Guid id, CancellationToken ct = default);
    Task<List<Teacher>> GetByIdsAsync(TenantContext tenant, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<List<Teacher>> GetByLastNameAsync(TenantContext tenant, string lastName, CancellationToken ct = default);
    Task<List<Teacher>> GetForClassAsync(TenantContext tenant, Guid classId, CancellationToken ct = default);
    Task AddAsync(
        TenantContext tenant,
        Teacher teacher,
        Guid? membershipId,
        CancellationToken ct = default
    );
    Task UpdateAsync(TenantContext tenant, Teacher teacher, CancellationToken ct = default);
    Task DeleteAsync(TenantContext tenant, Guid id, CancellationToken ct = default);
}
