using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Calendar;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalendarConfirmImport(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] long ExpectedRevision,
    string Digest,
    [property: JsonRequired] bool AcknowledgeWarnings,
    string Reason
);
