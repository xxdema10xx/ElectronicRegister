using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

// Context comes exclusively from the existing scoped ICurrentTenant, never a DTO.
public interface IInstitutionStudentManager
{
    Task<List<StudentDto>> GetForClassAsync(Guid classId, CancellationToken ct = default);
    Task<List<StudentDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<StudentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<StudentDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<List<StudentDto>> GetByLastNameAsync(string name, CancellationToken ct = default);
    Task<StudentDto?> GetOwnProfileAsync(CancellationToken ct = default);
    Task<Guid> AddAsync(
        CreateStudentDto dto,
        Guid? classId = null,
        Guid? membershipId = null,
        CancellationToken ct = default
    );
    Task UpdateAsync(Guid id, UpdateStudentDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
