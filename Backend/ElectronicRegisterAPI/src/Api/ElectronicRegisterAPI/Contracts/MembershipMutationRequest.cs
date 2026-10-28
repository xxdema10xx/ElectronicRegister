using ElectronicRegisterAPI.Domain.DTOs.Membership;
using ElectronicRegisterAPI.Api.Serialization;
using ElectronicRegisterAPI.Domain.Results;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MembershipMutationRequest
{
    [JsonRequired]
    public Guid EventId { get; init; }

    [JsonRequired]
    public long ExpectedRevision { get; init; }

    [JsonRequired, JsonConverter(typeof(StrictCatalogEnumConverter<MembershipMutationAction>))]
    public MembershipMutationAction Action { get; init; }

    [JsonConverter(typeof(StrictCatalogEnumConverter<InstitutionRole>))]
    public InstitutionRole? Role { get; init; }

    [JsonRequired]
    public string Reason { get; init; } = null!;

    public MembershipMutation ToMutation(Guid membershipId) => new(membershipId, EventId, ExpectedRevision, Action, Role, Reason);
}
