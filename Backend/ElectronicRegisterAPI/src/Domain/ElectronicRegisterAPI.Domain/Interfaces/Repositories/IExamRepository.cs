using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;
public interface IExamRepository
{
    Task<int> CountAsync();
    Task<List<Exam>> GetAllAsync();
    Task<Exam?> GetByIdAsync(Guid id);
    Task<List<Exam>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<List<Exam>> GetByDateAsync(DateOnly date);
    Task AddAsync(Exam exam);
    Task UpdateAsync(Exam exam);
    Task DeleteAsync(Exam exam);
}