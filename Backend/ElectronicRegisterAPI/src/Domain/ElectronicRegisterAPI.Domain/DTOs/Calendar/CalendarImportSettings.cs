using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarImportSettings(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] int Year,
    [property: JsonRequired] int Month,
    [property: JsonRequired] IReadOnlyList<Guid> ClassIds,
    [property: JsonRequired] CalendarImportMode Mode,
    IReadOnlyList<CalendarColumnMapping>? Mapping,
    string Reason
);
