using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ITeacherRepository
{
    Task<int> CountAsync();
    Task<List<Teacher>> GetAllAsync();
    Task<Teacher?> GetByIdAsync(Guid id);
    Task<List<Teacher>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task AddAsync(Teacher teacher);
    Task UpdateAsync(Teacher teacher);
    Task DeleteAsync(Teacher teacher);
}