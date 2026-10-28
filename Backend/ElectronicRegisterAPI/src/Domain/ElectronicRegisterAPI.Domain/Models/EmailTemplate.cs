using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Policies;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailTemplate(
    string LocalPartTemplate,
    string Domain,
    EmailNamingRules Names,
    EmailCollisionStrategy Collision = EmailCollisionStrategy.Reject,
    string NumericSuffix = "{n}",
    int SuffixStart = 2,
    int MaximumAttempts = 32
);
