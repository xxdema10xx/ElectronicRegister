using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;
public interface IStudyPathRepository
{
    Task<int> CountAsync();
    Task<StudyPath?> GetByIdAsync(Guid id);
    Task<List<StudyPath>> GetAllAsync();
    Task<StudyPath?> GetByNameAsync(string name);
    Task<bool> HasClassesAsync(Guid id);
    Task AddAsync(StudyPath studyPath);
    Task UpdateAsync(StudyPath studyPath);
    Task DeleteAsync(StudyPath studyPath);
}
