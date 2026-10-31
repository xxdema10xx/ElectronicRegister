namespace ElectronicRegisterAPI.Domain.Models;

public sealed record StudentEnrollment(
    Guid Id,
    Guid InstitutionId,
    Guid StudentId,
    Guid CourseEditionId,
    DateOnly EnrollmentDate,
    DateOnly? CompletionDate,
    string Status
);
