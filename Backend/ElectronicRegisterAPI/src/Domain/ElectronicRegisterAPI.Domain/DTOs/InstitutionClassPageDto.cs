using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionClassPageDto(
    IReadOnlyList<InstitutionClassDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
