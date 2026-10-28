using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarImportImpact(
    Guid ClassId,
    Guid ModuleId,
    long PlannedMinutes,
    long PublishedMinutes,
    long DraftMinutesAfterImport,
    long IncomingMinutes
)
{
    public long ProjectedScheduledMinutes => PublishedMinutes + DraftMinutesAfterImport;
    public string? Warning => ProjectedScheduledMinutes < PlannedMinutes ? "calendar.under-scheduled" : ProjectedScheduledMinutes > PlannedMinutes ? "calendar.over-scheduled" : null;
}
