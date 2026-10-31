using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionGovernanceManager
{
    Task<PlatformResult> ExecuteAsync(SignedPlatformOperation operation, CancellationToken ct = default);
}
