using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IStudyPathManager
    {
        Task<int> CountAsync();
        Task<List<StudyPathDto>> GetAllAsync();
        Task<StudyPathDto?> GetByIdAsync(Guid id);
        Task<StudyPathDto?> GetStudyPathByNameAsync(string name);
        Task<bool> AddAsync(CreateStudyPathDto dto);
        Task<bool> UpdateAsync(Guid id, UpdateStudyPathDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
