using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

// Reject immutable or authorization fields, including InstitutionId, instead of silently ignoring them.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCourseEditionDto(
    [Required] Guid StudyPathId,
    [Required, StringLength(64)] string Code,
    [Required, StringLength(200)] string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    [Required] string Status
);
