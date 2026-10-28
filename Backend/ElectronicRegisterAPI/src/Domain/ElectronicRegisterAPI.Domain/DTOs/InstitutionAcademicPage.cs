using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionAcademicPage<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
