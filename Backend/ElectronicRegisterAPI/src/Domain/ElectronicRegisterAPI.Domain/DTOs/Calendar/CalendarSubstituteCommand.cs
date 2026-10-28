using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarSubstituteCommand(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] long ExpectedRevision,
    [property: JsonRequired] Guid TeacherId,
    string Reason
);
