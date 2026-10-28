using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarLessonInput(
    [property: JsonRequired] Guid ClassId,
    [property: JsonRequired] Guid ModuleId,
    [property: JsonRequired] Guid TeacherId,
    [property: JsonRequired] DateOnly Date,
    [property: JsonRequired] TimeOnly StartsAt,
    [property: JsonRequired] TimeOnly EndsAt,
    string? SubstitutionReason = null,
    string? Room = null,
    string? Description = null,
    string? ExternalReference = null,
    Guid? RecoveryOfLessonId = null
);
