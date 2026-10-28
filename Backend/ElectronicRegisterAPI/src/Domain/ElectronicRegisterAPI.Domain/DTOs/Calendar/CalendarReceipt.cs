using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

public sealed record CalendarReceipt(
    Guid EventId,
    Guid ResourceId,
    long Revision,
    IReadOnlyList<Guid>? LessonIds = null
);
