using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInstitutionBienniumStudyAreaDto(Guid BienniumId, Guid StudyAreaId);
