using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IOnboardingRepository
{
    Task<PlatformResult> PlatformAsync(SignedPlatformOperation operation, CancellationToken ct);
    Task<InvitationResult> InviteAsync(TenantContext actor, InviteMember request, CancellationToken ct);
    Task<InvitationResult> ChangeAsync(
        TenantContext actor,
        ChangeInvitation request,
        bool revoke,
        CancellationToken ct
    );
    Task<InvitationResult> AcceptAsync(string selectionTicket, AcceptInvitation request, CancellationToken ct);
}
