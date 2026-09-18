namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IBienniumService
{
    void EnsureValidBienniumValue(int startYear, int endYear);
    Task EnsureBienniumExistsAsync(Guid id);
}
