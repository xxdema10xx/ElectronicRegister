using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionGovernanceRepository
{
    Task<PlatformResult> ExecuteAsync(SignedPlatformOperation operation, CancellationToken ct);
}
