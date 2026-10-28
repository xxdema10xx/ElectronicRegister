using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionBienniumStudyAreaDto(
    Guid Id,
    Guid BienniumId,
    Guid StudyAreaId,
    long Revision
);
