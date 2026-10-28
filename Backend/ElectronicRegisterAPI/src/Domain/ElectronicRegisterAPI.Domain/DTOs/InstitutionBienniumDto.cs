using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionBienniumDto(
    Guid Id,
    DateOnly StartYear,
    DateOnly EndYear,
    long Revision
);
