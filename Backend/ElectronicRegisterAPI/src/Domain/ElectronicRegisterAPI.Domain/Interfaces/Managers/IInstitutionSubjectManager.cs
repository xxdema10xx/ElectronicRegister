using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionSubjectManager
{
    Task<List<SubjectDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<SubjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<SubjectDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<SubjectDto?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<List<SubjectDto>> GetByTeacherAsync(Guid teacherId, CancellationToken ct = default);
    Task<Guid> AddAsync(CreateSubjectDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateSubjectDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
