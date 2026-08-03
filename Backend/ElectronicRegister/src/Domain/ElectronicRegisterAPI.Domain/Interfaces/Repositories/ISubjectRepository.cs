using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ISubjectRepository
{
    Task<int> CountAsync(Guid? teacherId = null);
    Task<List<SubjectDto>> GetAllAsync(Guid? teacherId = null);
    Task<SubjectDto?> GetByIdAsync(Guid id);
    Task<List<SubjectDto>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<SubjectDto?> GetByNameAsync(string name);
    Task<List<SubjectDto>> GetByTeacherIdAsync(Guid teacherId);
    Task<bool> ExistsForTeacherAsync(Guid teacherId);
    Task AddAsync(SubjectDto subjectDto);
    Task UpdateAsync(SubjectDto subjectDto);
    Task DeleteAsync(Guid id);
}