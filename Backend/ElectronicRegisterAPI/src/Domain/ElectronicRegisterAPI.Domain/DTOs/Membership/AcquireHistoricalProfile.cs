using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs.Membership;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcquireHistoricalProfile(
    Guid EventId,
    [property: JsonRequired] long ExpectedRevision,
    Guid ProfileId,
    string Kind,
    string Reason
);
