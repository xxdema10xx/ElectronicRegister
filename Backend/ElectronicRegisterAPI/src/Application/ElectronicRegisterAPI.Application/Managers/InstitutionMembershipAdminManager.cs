using ElectronicRegisterAPI.Domain.DTOs.Membership;
using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionMembershipAdminManager(InstitutionSchoolAuthorization authorization, IInstitutionMembershipDirectoryRepository repository, IInstitutionMembershipMutationRepository mutations) : IInstitutionMembershipAdminManager
{
    public async Task<ElectronicRegisterAPI.Domain.Results.MembershipMutationResult> MutateAsync(ElectronicRegisterAPI.Domain.DTOs.Membership.MembershipMutation request, CancellationToken ct = default)
    {
        var actor = await authorization.AdminAsync(ct);
        ElectronicRegisterAPI.Domain.Policies.MembershipMutationRules.Validate(request);
        // Repository repeats authorization under the institution mutex, including replay.
        return await mutations.MutateAsync(actor, request, ct);
    }

    public async Task<IReadOnlyList<InstitutionMemberDto>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? status = null,
        string? role = null,
        string? search = null,
        CancellationToken ct = default
    )
    {
        var tenant = await authorization.AdminAsync(ct);
        if (pageNumber < 1 || pageSize is < 1 or > 100 || (long)(pageNumber - 1) * pageSize > int.MaxValue || (status is not null && !Enum.GetNames<MembershipStatus>().Any(s => s.ToLowerInvariant() == status)) || (role is not null && !Enum.GetNames<InstitutionRole>().Contains(role)) || (search is not null && (string.IsNullOrWhiteSpace(search) || search.Length > 100)))
            throw new ArgumentException("Filtri directory non validi.");
        return await repository.ListAsync(tenant.InstitutionId, pageNumber, pageSize, status, role, search?.Trim(), ct);
    }

    public async Task<InstitutionMemberDto> GetAsync(Guid membershipId, CancellationToken ct = default)
    {
        var tenant = await authorization.AdminAsync(ct);
        return await repository.GetAsync(tenant.InstitutionId, membershipId, ct) ?? throw new KeyNotFoundException();
    }
}
