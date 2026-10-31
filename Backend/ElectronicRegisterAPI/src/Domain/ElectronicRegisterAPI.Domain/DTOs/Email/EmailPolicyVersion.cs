using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Email;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailPolicyVersion(
    Guid PolicyId,
    EmailCategory Category,
    long Revision,
    string State,
    EmailTemplate Template,
    Guid ActorUserId,
    Guid ActorMembershipId,
    DateTime CreatedAtUtc,
    string DomainVerificationState = "ConfiguredUnverified"
);
