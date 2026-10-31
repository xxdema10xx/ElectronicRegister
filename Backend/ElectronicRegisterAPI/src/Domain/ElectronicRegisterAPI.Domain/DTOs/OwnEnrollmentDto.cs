using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record OwnEnrollmentDto(
    Guid EnrollmentId,
    Guid CourseEditionId,
    string CourseEditionCode,
    string CourseEditionName,
    DateOnly EnrollmentDate,
    DateOnly? CompletionDate,
    string Status
);
