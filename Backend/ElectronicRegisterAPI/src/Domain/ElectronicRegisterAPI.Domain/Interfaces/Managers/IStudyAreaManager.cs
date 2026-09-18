using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IStudyAreaManager
    {
        Task<int> CountAsync();
        Task<List<StudyAreaDto>> GetAllAsync();
        Task<StudyAreaDto?> GetByIdAsync(Guid id);
        Task<StudyAreaDto?> GetStudyAreaByNameAsync(string name);
        Task<bool> AddAsync(CreateStudyAreaDto dto);
        Task<bool> UpdateAsync(Guid id, UpdateStudyAreaDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
