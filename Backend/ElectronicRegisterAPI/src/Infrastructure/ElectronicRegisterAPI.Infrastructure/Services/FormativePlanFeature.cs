using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Infrastructure.Services;

public sealed class FormativePlanFeature(Func<bool> availability) : RuntimeFeatureAvailability(availability), IFormativePlanFeature
{
}
