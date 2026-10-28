using ElectronicRegisterAPI.Domain.DTOs.Governance;
using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionGovernanceManager(IInstitutionGovernanceRepository repository) : IInstitutionGovernanceManager
{
    public Task<PlatformResult> ExecuteAsync(SignedPlatformOperation operation, CancellationToken ct = default) => repository.ExecuteAsync(operation, ct);
}
