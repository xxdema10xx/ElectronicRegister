namespace ElectronicRegisterAPI.Domain.Models;

// Internal persistence contracts. Caller authorization belongs to MT-4B managers.
public sealed record CourseEdition(
    Guid Id,
    Guid InstitutionId,
    Guid StudyPathId,
    string Code,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status
);
