using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailPolicyWrite(
    Guid EventId,
    long ExpectedRevision,
    string State,
    EmailTemplate Template,
    string Reason
);
