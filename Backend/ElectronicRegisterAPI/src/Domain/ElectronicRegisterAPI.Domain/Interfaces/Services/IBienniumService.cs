namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IBienniumService
{
    void EnsureValidBienniumValue(DateOnly startYear, DateOnly endYear);
    Task EnsureBienniumExistsAsync(Guid id);
    Task EnsureBienniumCanBeDeletedAsync(Guid bienniumId);
}
