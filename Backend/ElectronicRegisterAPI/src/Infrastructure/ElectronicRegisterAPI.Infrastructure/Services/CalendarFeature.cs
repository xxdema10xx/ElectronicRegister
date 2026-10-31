using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Infrastructure.Services;

public sealed class CalendarFeature(Func<bool> availability) : RuntimeFeatureAvailability(availability), ICalendarFeature
{
}
