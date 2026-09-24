namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IStudyAreaService
{
    void EnsureValidStudyAreaName(string? name);
    void EnsureValidStudyAreaDescription(string? description);
    Task EnsureStudyAreaExistsAsync(Guid id);
}
