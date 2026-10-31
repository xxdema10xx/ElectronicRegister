namespace ElectronicRegisterAPI.Domain.Models;

public sealed record CalendarTeachingBinding(
    Guid InstitutionId,
    Guid CourseEditionId,
    Guid ClassId,
    Guid VersionId,
    Guid ModuleId,
    Guid SubjectId,
    Guid OriginalTeacherId,
    Guid ScheduledTeacherId,
    string? SubstitutionReason
);
