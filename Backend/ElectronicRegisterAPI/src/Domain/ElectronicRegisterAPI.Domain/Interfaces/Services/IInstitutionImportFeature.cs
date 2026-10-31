using ElectronicRegisterAPI.Domain.Configuration;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IInstitutionImportFeature
{
    InstitutionImportFeature Settings { get; }

    bool IsEnabled { get; }
}
