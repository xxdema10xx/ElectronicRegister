using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateInstitutionCampusDto(
    string Name,
    string? Address,
    string? City,
    string? Province,
    [property: JsonRequired] bool IsActive
);
