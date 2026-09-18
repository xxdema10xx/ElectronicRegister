using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IBienniumManager
    {
        Task<int> CountAsync();
        Task<List<BienniumDto>> GetAllAsync();
        Task<List<BienniumDto>> GetActiveBienniaAsync();
        Task<BienniumDto?> GetByIdAsync(Guid id);
        Task<BienniumDto?> GetBienniumByStartYearAsync(int startYear);
        Task<BienniumDto?> GetBienniumByEndYearAsync(int endYear);
        Task<bool> AddAsync(CreateBienniumDto dto);
        Task<bool> UpdateAsync(Guid id, UpdateBienniumDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
