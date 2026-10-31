using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionTeacherManager
{
    Task<List<TeacherDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<TeacherDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TeacherDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<List<TeacherDto>> GetByLastNameAsync(string name, CancellationToken ct = default);
    Task<List<TeacherDto>> GetForClassAsync(Guid classId, CancellationToken ct = default);
    Task<TeacherDto?> GetOwnProfileAsync(CancellationToken ct = default);
    Task<Guid> AddAsync(CreateTeacherDto dto, Guid? membershipId = null, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateTeacherDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
