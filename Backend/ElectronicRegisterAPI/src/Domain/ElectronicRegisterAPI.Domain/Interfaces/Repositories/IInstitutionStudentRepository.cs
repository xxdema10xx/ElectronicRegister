using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

// Application managers must verify the immutable context against current membership.
// No institution-id overloads, historical global roles, or unscoped school lookups.
public interface IInstitutionStudentRepository
{
    Task<List<Student>> GetForClassAsync(TenantContext tenant, Guid classId, CancellationToken ct = default);
    Task<List<Student>> GetAllAsync(TenantContext tenant, CancellationToken ct = default);
    Task<int> CountAsync(TenantContext tenant, CancellationToken ct = default);
    Task<Student?> GetByIdAsync(TenantContext tenant, Guid id, CancellationToken ct = default);
    Task<List<Student>> GetByIdsAsync(TenantContext tenant, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<List<Student>> GetByLastNameAsync(TenantContext tenant, string lastName, CancellationToken ct = default);
    Task<Student?> GetOwnAsync(TenantContext tenant, Guid studentId, CancellationToken ct = default);
    Task AddAsync(
        TenantContext tenant,
        Student student,
        Guid? membershipId,
        CancellationToken ct = default
    );
    Task UpdateAsync(TenantContext tenant, Student student, CancellationToken ct = default);
    Task DeleteAsync(TenantContext tenant, Guid id, CancellationToken ct = default);
}
