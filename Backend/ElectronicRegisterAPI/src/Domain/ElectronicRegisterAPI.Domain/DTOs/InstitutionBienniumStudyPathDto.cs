using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionBienniumStudyPathDto(
    Guid Id,
    Guid BienniumStudyAreaId,
    Guid StudyPathId,
    long Revision
);
