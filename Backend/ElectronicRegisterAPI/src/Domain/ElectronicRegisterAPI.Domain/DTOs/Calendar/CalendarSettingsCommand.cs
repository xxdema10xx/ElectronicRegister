using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarSettingsCommand(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] long ExpectedRevision,
    string TimeZoneId,
    [property: JsonRequired] int ApprovalLeadDays,
    string Reason
);
