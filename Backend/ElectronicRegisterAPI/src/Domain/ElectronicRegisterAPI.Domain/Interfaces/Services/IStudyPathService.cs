namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IStudyPathService
{
    Task EnsureValidStudyPathIdAsync(Guid id);
    void EnsureValidStudyPathName(string? name);
    void EnsureValidStudyPathDescription(string? description);
    Task StudyPathExistsByNameAsync(string name);
}
