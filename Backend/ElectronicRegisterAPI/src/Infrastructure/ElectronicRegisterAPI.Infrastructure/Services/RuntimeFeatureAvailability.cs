using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Infrastructure.Services;

public class RuntimeFeatureAvailability(Func<bool> availability) : IFeatureAvailability
{
    public bool Enabled => availability();
}
