using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IStudyAreaService
{
    void EnsureValidStudyAreaName(string? name);
    void EnsureValidStudyAreaDescription(string? description);
    Task<StudyArea> EnsureStudyAreaExistsAsync(Guid id);
    Task<StudyArea> EnsureStudyAreaCanBeDeletedAsync(Guid id);
}
