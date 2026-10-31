using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionOnboardingManager
{
    Task<PlatformResult> PlatformAsync(SignedPlatformOperation operation, CancellationToken ct = default);
    Task<InvitationResult> InviteAsync(InviteMember request, CancellationToken ct = default);
    Task<InvitationResult> RevokeAsync(ChangeInvitation request, CancellationToken ct = default);
    Task<InvitationResult> ReissueAsync(ChangeInvitation request, CancellationToken ct = default);
    Task<InvitationResult> AcceptAsync(string selectionTicket, AcceptInvitation request, CancellationToken ct = default);
}
