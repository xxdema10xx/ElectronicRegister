using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionStudyPathDto(
    Guid Id,
    string Name,
    string? Description,
    long Revision
);
