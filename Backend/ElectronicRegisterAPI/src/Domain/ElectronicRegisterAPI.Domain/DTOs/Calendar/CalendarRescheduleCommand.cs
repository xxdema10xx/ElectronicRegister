using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarRescheduleCommand(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] long ExpectedRevision,
    [property: JsonRequired] DateOnly Date,
    [property: JsonRequired] TimeOnly StartsAt,
    [property: JsonRequired] TimeOnly EndsAt,
    string Reason
);
