using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;
public interface IStudyAreaRepository
{
    Task<int> CountAsync();
    Task<List<StudyArea>> GetAllAsync();
    Task<StudyArea?> GetByIdAsync(Guid studyAreaId);
    Task<StudyArea?> GetStudyAreaByNameAsync(string name);
    Task AddAsync(StudyArea studyArea);
    Task UpdateAsync(StudyArea studyArea);
    Task DeleteAsync(StudyArea studyArea);
}
