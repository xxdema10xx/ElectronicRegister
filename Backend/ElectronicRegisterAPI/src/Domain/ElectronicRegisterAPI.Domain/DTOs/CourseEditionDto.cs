using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record CourseEditionDto(
    Guid Id,
    Guid InstitutionId,
    Guid StudyPathId,
    string Code,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Version
);
