using ElectronicRegisterAPI.Domain.Models;

public interface ISubjectRepository
{
    Task<int> CountAsync();
    Task<List<Subject>> GetAllAsync();
    Task<Subject?> GetByIdAsync(Guid id);
    Task<List<Subject>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<Subject?> GetByNameAsync(string name);
    Task AddAsync(Subject subject);
    Task UpdateAsync(Subject subject);
    Task DeleteAsync(Subject subject);
}