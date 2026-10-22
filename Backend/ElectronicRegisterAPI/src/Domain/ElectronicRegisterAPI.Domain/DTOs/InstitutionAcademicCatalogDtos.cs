using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;
public sealed record InstitutionAcademicPage<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);
public sealed record InstitutionStudyAreaDto(Guid Id, string Name, string Description, long Revision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInstitutionStudyAreaDto(string Name, string Description);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateInstitutionStudyAreaDto(string Name, string Description);
public sealed record InstitutionStudyPathDto(Guid Id, string Name, string? Description, long Revision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInstitutionStudyPathDto(string Name, string? Description);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateInstitutionStudyPathDto(string Name, string? Description);
public sealed record InstitutionBienniumDto(Guid Id, DateOnly StartYear, DateOnly EndYear, long Revision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInstitutionBienniumDto(DateOnly StartYear, DateOnly EndYear);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateInstitutionBienniumDto(DateOnly StartYear, DateOnly EndYear);
public sealed record InstitutionBienniumStudyAreaDto(Guid Id, Guid BienniumId, Guid StudyAreaId, long Revision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInstitutionBienniumStudyAreaDto(Guid BienniumId, Guid StudyAreaId);
public sealed record InstitutionBienniumStudyPathDto(Guid Id, Guid BienniumStudyAreaId, Guid StudyPathId, long Revision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInstitutionBienniumStudyPathDto(Guid BienniumStudyAreaId, Guid StudyPathId);
