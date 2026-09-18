namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IStudyPathService
{
    void EnsureValidStudyPathName(string name);
    void EnsureValidStudyPathDescription(string description);
    Task EnsureStudyPathExistsAsync(Guid id);
}
