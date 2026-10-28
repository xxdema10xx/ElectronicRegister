using ElectronicRegisterAPI.Domain.DTOs.Membership;
using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

// Operational IAM only. Does not expose the privileged Provisioning writer.
public interface IInstitutionMembershipMutationRepository
{
    Task<MembershipMutationResult> MutateAsync(TenantContext actor, MembershipMutation request, CancellationToken ct = default);
}
