using ElectronicRegisterAPI.Domain.Results;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Api.Contracts;

public sealed record MembershipMutationResponse(
    Guid EventId,
    long Revision,
    string Status,
    IReadOnlyList<string> Roles
)
{
    public static MembershipMutationResponse From(MembershipMutationResult result) => new(result.EventId, result.Revision, result.Status.ToString().ToLowerInvariant(), result.Roles.Select(r => r.ToString()).ToArray());
}
