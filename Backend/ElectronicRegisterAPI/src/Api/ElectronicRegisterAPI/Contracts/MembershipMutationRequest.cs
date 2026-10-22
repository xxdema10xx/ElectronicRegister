using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
namespace ElectronicRegisterAPI.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MembershipMutationRequest
{
    [JsonRequired] public Guid EventId { get; init; }
    [JsonRequired] public long ExpectedRevision { get; init; }
    [JsonRequired, JsonConverter(typeof(StrictCatalogEnumConverter<MembershipMutationAction>))]
    public MembershipMutationAction Action { get; init; }
    [JsonConverter(typeof(StrictCatalogEnumConverter<InstitutionRole>))]
    public InstitutionRole? Role { get; init; }
    [JsonRequired] public string Reason { get; init; } = null!;

    public MembershipMutation ToMutation(Guid membershipId) =>
        new(membershipId,EventId,ExpectedRevision,Action,Role,Reason);
}

// Local to this contract: no numeric, case-insensitive or composite enum aliases.
public sealed class StrictCatalogEnumConverter<T> : JsonConverter<T> where T:struct,Enum
{
    public override T Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {
        if(reader.TokenType!=JsonTokenType.String || reader.GetString() is not {} text ||
            !Enum.GetNames<T>().Contains(text,StringComparer.Ordinal))
            throw new JsonException("Valore del catalogo non valido.");
        return Enum.Parse<T>(text);
    }
    public override void Write(Utf8JsonWriter writer,T value,JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}

public sealed record MembershipMutationResponse(Guid EventId,long Revision,string Status,IReadOnlyList<string> Roles)
{
    public static MembershipMutationResponse From(MembershipMutationResult result) =>
        new(result.EventId,result.Revision,result.Status.ToString().ToLowerInvariant(),result.Roles.Select(r=>r.ToString()).ToArray());
}
