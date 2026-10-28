namespace ElectronicRegisterAPI.Domain.Models;

public sealed record CalendarSubjectAssignment(
    Guid InstitutionId,
    Guid ClassId,
    Guid SubjectId,
    Guid TeacherId
);
