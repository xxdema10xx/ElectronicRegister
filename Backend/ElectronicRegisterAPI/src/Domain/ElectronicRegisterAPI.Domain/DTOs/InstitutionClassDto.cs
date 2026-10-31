using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionClassDto(
    Guid Id,
    string Name,
    Guid BienniumStudyPathId,
    Guid? CourseEditionId,
    long Revision
);
