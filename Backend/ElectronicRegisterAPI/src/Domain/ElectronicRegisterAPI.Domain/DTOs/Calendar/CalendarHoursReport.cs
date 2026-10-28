using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarHoursReport(
    Guid ClassId,
    Guid CourseEditionId,
    Guid VersionId,
    IReadOnlyList<CalendarModuleHours> Modules,
    IReadOnlyList<CalendarUnitHours> Units,
    IReadOnlyList<CalendarMonthlyHours> Months
)
{
    public long PlannedMinutes => Modules.Sum(m => m.PlannedMinutes);
    public long ScheduledMinutes => Modules.Sum(m => m.ScheduledMinutes);
    public long DeliveredMinutes => Modules.Sum(m => m.DeliveredMinutes);
}
