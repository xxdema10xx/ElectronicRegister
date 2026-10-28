using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInstitutionCampusDto(
    string Name,
    string? Address = null,
    string? City = null,
    string? Province = null,
    bool IsActive = true
);
