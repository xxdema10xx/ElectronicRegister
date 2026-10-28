using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarUnitHours(
    Guid UnitId,
    long PlannedMinutes,
    long ScheduledMinutes,
    long DeliveredMinutes
);
