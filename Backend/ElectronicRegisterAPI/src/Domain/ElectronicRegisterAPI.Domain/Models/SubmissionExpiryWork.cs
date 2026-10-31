using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Domain.Models;

// Opaque capability: created only by persistence after resolving a coherent owner.
// No public constructor, setters, JSON binding, membership or interactive privileges.
public sealed class SubmissionExpiryWork
{
    public Guid InstitutionId { get; }
    public Guid SubmissionId { get; }
    public Guid ExamId { get; }
    public Guid StudentId { get; }
    public Guid ClassSubjectId { get; }

    internal SubmissionExpiryWork(
        Guid institution,
        Guid submission,
        Guid exam,
        Guid student,
        Guid assignment
    )
    {
        if (new[]
        {
            institution,
            submission,
            exam,
            student,
            assignment
        }.Any(id => id == Guid.Empty))
            throw new UnauthorizedAccessException();
        InstitutionId = institution;
        SubmissionId = submission;
        ExamId = exam;
        StudentId = student;
        ClassSubjectId = assignment;
    }
}
