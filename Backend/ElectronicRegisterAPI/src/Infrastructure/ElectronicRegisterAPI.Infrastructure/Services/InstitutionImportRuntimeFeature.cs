using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Infrastructure.Services;

public sealed class InstitutionImportRuntimeFeature(InstitutionImportFeature settings, IFeatureAvailability? availability = null) : IInstitutionImportFeature
{
    public InstitutionImportFeature Settings => settings;
    public bool IsEnabled => availability?.Enabled ?? settings.IsEnabled;
}
