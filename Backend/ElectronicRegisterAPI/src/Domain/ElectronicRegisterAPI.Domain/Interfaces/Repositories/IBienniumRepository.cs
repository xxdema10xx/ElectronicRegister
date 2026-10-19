using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;
public interface IBienniumRepository
{
    Task<int> CountAsync();
    Task<IEnumerable<Biennium>> GetAllAsync();
    Task<IEnumerable<Biennium>> GetActiveBienniaAsync();
    Task<IEnumerable<Biennium>> GetInactiveBienniaAsync();
    Task<bool> HasStudyAreasAsync(Guid bienniumId);
    Task<Biennium?> GetByIdAsync(Guid bienniumId);
    Task<Biennium?> GetBienniumByStartYearAsync(int startYear);
    Task<Biennium?> GetBienniumByEndYearAsync(int endYear);
    Task AddAsync(Biennium biennium);
    Task UpdateAsync(Biennium biennium);
    Task DeleteAsync(Biennium biennium);
}

