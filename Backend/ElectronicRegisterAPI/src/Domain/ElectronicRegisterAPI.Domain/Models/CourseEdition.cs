namespace ElectronicRegisterAPI.Domain.Models;

// Internal persistence contracts. Caller authorization belongs to MT-4B managers.
public sealed record CourseEdition(Guid Id, Guid InstitutionId, Guid StudyPathId, string Code,
    string Name, DateOnly? StartDate, DateOnly? EndDate, string Status);
public sealed record StudentEnrollment(Guid Id, Guid InstitutionId, Guid StudentId,
    Guid CourseEditionId, DateOnly EnrollmentDate, DateOnly? CompletionDate, string Status);

public static class CourseStructureValidation
{
    public static void Edition(CourseEdition value)
    {
        if (value.Id == Guid.Empty || value.StudyPathId == Guid.Empty || value.InstitutionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(value.Code) || value.Code.Length > 64 ||
            string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 200 ||
            value.StartDate is { Year: < 1000 } || value.EndDate is { Year: < 1000 } ||
            value.Status is not ("planned" or "active" or "completed" or "cancelled") ||
            value.StartDate is { } start && value.EndDate is { } end && end < start)
            throw new ArgumentException("Edizione non valida.");
    }
    public static void Enrollment(StudentEnrollment value)
    {
        if (value.Id == Guid.Empty || value.InstitutionId == Guid.Empty || value.StudentId == Guid.Empty ||
            value.CourseEditionId == Guid.Empty || value.EnrollmentDate.Year < 1000 ||
            value.CompletionDate < value.EnrollmentDate ||
            value.Status is not ("enrolled" or "completed" or "withdrawn" or "cancelled"))
            throw new ArgumentException("Iscrizione non valida.");
    }
}
