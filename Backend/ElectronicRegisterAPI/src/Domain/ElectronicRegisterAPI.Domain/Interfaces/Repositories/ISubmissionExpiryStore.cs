using ElectronicRegisterAPI.Domain.Persistence;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ISubmissionExpiryStore
{
    Task<List<ExpiryCandidate>> SelectAsync(
        DateTime now,
        ExpiryCandidate? after,
        int take,
        CancellationToken ct
    );
    Task<SubmissionExpiryWork?> ResolveAsync(Guid submissionId, Guid institutionId, CancellationToken ct);
    ISubmissionExpiryRepositories Bind(SubmissionExpiryWork work, CancellationToken ct);
}
