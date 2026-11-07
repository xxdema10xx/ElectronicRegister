using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Api.Contracts;

// No institution field: the institution always comes from the access token. Unknown
// fields (an extra "role", an "institutionId") are rejected for every caller.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateInstitutionAccountRequest
{
    [JsonRequired]
    public Guid EventId { get; init; }

    [JsonRequired]
    public string Email { get; init; } = "";

    [JsonRequired]
    public string[] Roles { get; init; } = [];

    public Guid? StudentId { get; init; }
    public Guid? TeacherId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public Guid? ClassId { get; init; }

    [JsonRequired]
    public string Reason { get; init; } = "";

    internal CreateInstitutionAccount ToDomain()
    {
        if (Roles is null || Roles.Any(r => !Enum.GetNames<InstitutionRole>().Contains(r, StringComparer.Ordinal)))
            throw new ArgumentException();
        return new(EventId, Email ?? "", Roles.Select(Enum.Parse<InstitutionRole>).ToArray(), StudentId, TeacherId, FirstName, LastName, ClassId, Reason ?? "");
    }
}
