using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Application.Services;

internal sealed class InstitutionExpiryService(ISubmissionExpiryStore store, ITransactionManager transactions,
    ISubmissionGradingService grading, ITimeProvider time) : IInstitutionExpiryService
{
    public async Task ProcessAsync(ExpiryCandidate candidate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var work = await store.ResolveAsync(candidate.SubmissionId, candidate.InstitutionId, ct)
            ?? throw new KeyNotFoundException("Submission tecnica non disponibile.");
        var r = store.Bind(work, ct);
        var processor = new ExamSubmissionProcessor(transactions, r.Submissions, r.Exams, grading, r.Grades, r.Audit, time);
        await processor.ProcessExpiryAsync(work, ct);
    }
}
