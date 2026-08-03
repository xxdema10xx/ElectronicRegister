using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ISubjectRepository
{
    Task<int> CountAsync(Guid? teacherId = null);
    Task<List<Subject>> GetAllAsync(Guid? teacherId = null);
    Task<Subject?> GetByIdAsync(Guid id);
    Task<List<Subject>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<Subject?> GetByNameAsync(string name);
    Task<List<Subject>> GetByTeacherIdAsync(Guid teacherId);
    Task<bool> ExistsForTeacherAsync(Guid teacherId);
    Task AddAsync(Subject subject);
    Task UpdateAsync(Subject subject);
    Task DeleteAsync(Guid id);
}