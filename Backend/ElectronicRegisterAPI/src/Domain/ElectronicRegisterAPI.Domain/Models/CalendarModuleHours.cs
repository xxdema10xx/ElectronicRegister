using System.Globalization;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record CalendarModuleHours(
    Guid ClassId,
    Guid ModuleId,
    long PlannedMinutes,
    long ScheduledMinutes,
    long DeliveredMinutes
)
{
    public long RemainingToScheduleMinutes => Math.Max(0, PlannedMinutes - ScheduledMinutes);
    public long RemainingToDeliverMinutes => Math.Max(0, PlannedMinutes - DeliveredMinutes);
    public long ScheduledExcessMinutes => Math.Max(0, ScheduledMinutes - PlannedMinutes);
    public long DeliveredExcessMinutes => Math.Max(0, DeliveredMinutes - PlannedMinutes);
    public IReadOnlyList<string> Warnings => new[]
    {
        ScheduledMinutes < PlannedMinutes ? "calendar.under-scheduled" : null,
        ScheduledMinutes > PlannedMinutes ? "calendar.over-scheduled" : null,
        DeliveredMinutes < PlannedMinutes ? "calendar.delivery-incomplete" : null,
        DeliveredMinutes > PlannedMinutes ? "calendar.over-delivered" : null
    }.OfType<string>().ToArray();
}
