using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record CourseEditionDto(Guid Id, Guid InstitutionId, Guid StudyPathId, string Code, string Name,
    DateOnly? StartDate, DateOnly? EndDate, string Status, DateTime CreatedAt, DateTime UpdatedAt, string Version);
public sealed record CampusDto(Guid CampusId, string Name, string? Address, string? City, string? Province, bool? IsActive);
public sealed record EditionClassDto(Guid ClassId, string Name, Guid BienniumStudyPathId, Guid CourseEditionId);
public sealed record StudentEnrollmentDto(Guid EnrollmentId, Guid CourseEditionId, string CourseEditionCode,
    string CourseEditionName, Guid StudentId, DateOnly EnrollmentDate, DateOnly? CompletionDate, string Status, long Revision = 0);
public sealed record OwnEnrollmentDto(Guid EnrollmentId, Guid CourseEditionId, string CourseEditionCode,
    string CourseEditionName, DateOnly EnrollmentDate, DateOnly? CompletionDate, string Status);

// Reject immutable or authorization fields, including InstitutionId, instead of silently ignoring them.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCourseEditionDto([Required] Guid StudyPathId, [Required, StringLength(64)] string Code,
    [Required, StringLength(200)] string Name, DateOnly? StartDate, DateOnly? EndDate, [Required] string Status);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateCourseEditionDto([Required, StringLength(64)] string Code, [Required, StringLength(200)] string Name,
    DateOnly? StartDate, DateOnly? EndDate, [Required, StringLength(64, MinimumLength = 64)] string Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinkEditionCampusDto(Guid CampusId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateStudentEnrollmentDto(Guid StudentId, Guid CourseEditionId, DateOnly EnrollmentDate,
    DateOnly? CompletionDate, [Required] string Status);
