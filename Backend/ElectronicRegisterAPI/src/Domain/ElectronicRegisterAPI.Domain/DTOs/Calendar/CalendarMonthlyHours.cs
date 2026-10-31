using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarMonthlyHours(
    int Year,
    int Month,
    long ScheduledMinutes,
    long DeliveredMinutes
);
