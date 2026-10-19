using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ElectronicRegisterAPI.Infrastructure")]

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
    internal SubmissionExpiryWork(Guid institution, Guid submission, Guid exam, Guid student, Guid assignment)
    {
        if (new[] { institution, submission, exam, student, assignment }.Any(id => id == Guid.Empty)) throw new UnauthorizedAccessException();
        InstitutionId = institution; SubmissionId = submission; ExamId = exam; StudentId = student; ClassSubjectId = assignment;
    }
}

public sealed record ExpiryCandidate(Guid SubmissionId, Guid InstitutionId, DateTime DeadlineAt);
public interface ISubmissionExpiryStore
{
    Task<List<ExpiryCandidate>> SelectAsync(DateTime now, ExpiryCandidate? after, int take, CancellationToken ct);
    Task<SubmissionExpiryWork?> ResolveAsync(Guid submissionId, Guid institutionId, CancellationToken ct);
    ISubmissionExpiryRepositories Bind(SubmissionExpiryWork work, CancellationToken ct);
}
public interface ISubmissionExpiryRepositories
{
    IExamSubmissionRepository Submissions { get; }
    IExamRepository Exams { get; }
    IGradeRepository Grades { get; }
    IAuditRepository Audit { get; }
}
public interface ISubmissionExpiryBoundRepository
{
    bool Matches(SubmissionExpiryWork work);
}
public interface IInstitutionExpiryService
{
    Task ProcessAsync(ExpiryCandidate candidate, CancellationToken ct);
}
