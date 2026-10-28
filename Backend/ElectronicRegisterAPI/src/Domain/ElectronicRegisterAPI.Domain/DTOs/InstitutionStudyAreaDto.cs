using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionStudyAreaDto(
    Guid Id,
    string Name,
    string Description,
    long Revision
);
