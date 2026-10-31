using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionCampusDto(
    Guid Id,
    string Name,
    string? Address,
    string? City,
    string? Province,
    bool IsActive,
    long Revision
)
{
    public Guid CampusId => Id;
}
