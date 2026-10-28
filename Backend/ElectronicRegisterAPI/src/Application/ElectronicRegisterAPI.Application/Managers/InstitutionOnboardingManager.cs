using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionOnboardingManager(IOnboardingRepository repository, InstitutionSchoolAuthorization authorization) : IInstitutionOnboardingManager
{
    public Task<PlatformResult> PlatformAsync(SignedPlatformOperation operation, CancellationToken ct = default) => repository.PlatformAsync(operation, ct);
    public async Task<InvitationResult> InviteAsync(InviteMember request, CancellationToken ct = default) => await repository.InviteAsync(await authorization.AdminAsync(ct), request, ct);
    public async Task<InvitationResult> RevokeAsync(ChangeInvitation request, CancellationToken ct = default) => await repository.ChangeAsync(await authorization.AdminAsync(ct), request, true, ct);
    public async Task<InvitationResult> ReissueAsync(ChangeInvitation request, CancellationToken ct = default) => await repository.ChangeAsync(await authorization.AdminAsync(ct), request, false, ct);
    public Task<InvitationResult> AcceptAsync(string ticket, AcceptInvitation request, CancellationToken ct = default) => repository.AcceptAsync(ticket, request, ct);
}
