using ElectronicRegisterAPI.Domain.DTOs;
namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionMembershipAdminManager
{
    Task<IReadOnlyList<InstitutionMemberDto>> ListAsync(int pageNumber = 1, int pageSize = 20,
        string? status = null, string? role = null, string? search = null, CancellationToken ct = default);
    Task<InstitutionMemberDto> GetAsync(Guid membershipId, CancellationToken ct = default);
    Task<ElectronicRegisterAPI.Domain.Models.MembershipMutationResult> MutateAsync(
        ElectronicRegisterAPI.Domain.Models.MembershipMutation request, CancellationToken ct = default);
}
