using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record StudentEnrollmentDto(
    Guid EnrollmentId,
    Guid CourseEditionId,
    string CourseEditionCode,
    string CourseEditionName,
    Guid StudentId,
    DateOnly EnrollmentDate,
    DateOnly? CompletionDate,
    string Status,
    long Revision = 0
);
