using ElectronicRegisterAPI.Domain.Persistence;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IInstitutionExpiryService
{
    Task ProcessAsync(ExpiryCandidate candidate, CancellationToken ct);
}
