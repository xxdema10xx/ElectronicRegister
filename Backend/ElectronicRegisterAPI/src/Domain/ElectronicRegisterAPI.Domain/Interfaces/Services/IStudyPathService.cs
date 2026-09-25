using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IStudyPathService
{
    Task<StudyPath> EnsureValidStudyPathIdAsync(Guid id);
    void EnsureValidStudyPathName(string? name);
    void EnsureValidStudyPathDescription(string? description);
    Task StudyPathAlreadyExistsByNameAsync(string name);
    Task<StudyPath> EnsureStudyPathCanBeDeletedAsync(Guid id);
}
