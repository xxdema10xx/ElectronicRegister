using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateStudentEnrollmentDto(
    Guid StudentId,
    Guid CourseEditionId,
    DateOnly EnrollmentDate,
    DateOnly? CompletionDate,
    [Required] string Status
);
