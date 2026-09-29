using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IItsClassRepository
{
    Task<int> CountAsync();
    Task<List<ItsClass>> GetAllAsync();
    Task<ItsClass?> GetByIdAsync(Guid id);
    Task<List<ItsClass>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task <ItsClass?> GetByNameAsync(string name);
    Task AddAsync(ItsClass itsClass);
    Task UpdateAsync(ItsClass itsClass);
    Task DeleteAsync(ItsClass itsClass);
}
