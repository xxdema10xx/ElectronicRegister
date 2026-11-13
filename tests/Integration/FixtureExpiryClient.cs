using System;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Persistence;

namespace ElectronicRegisterAPI.Tests.Integration;

internal sealed class FixtureExpiryClient(ISubmissionExpiryStore store, IInstitutionExpiryService service)
{
    internal async Task ProcessAsync(Guid submission)
    {
        var work = await store.ResolveAsync(submission, SeedData.CoreInstitutionId, CancellationToken.None)
            ?? throw new UnauthorizedAccessException("No coherent fixture expiry scope.");
        await service.ProcessAsync(new ExpiryCandidate(submission, work.InstitutionId, DateTime.UtcNow), CancellationToken.None);
    }
}
