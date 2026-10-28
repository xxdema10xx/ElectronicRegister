using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailOverride(
    Guid EventId,
    long ExpectedRevision,
    long PolicyRevision,
    string Email,
    string Reason
);
