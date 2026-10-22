using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public enum PlatformAction { Provision, Suspend, Reactivate, Archive, RequestRecovery, ApproveRecovery, ReissueFirstAdmin, RevokeFirstAdmin, RevokeRecovery, ReissueRecovery }
public sealed record PlatformIntent(Guid InstitutionId, Guid RecipientUserId, string Name, string Code,
    string LegalIdentity, string EvidenceReference, string Reason, Guid RecoveryId = default);
public sealed record SignedPlatformOperation(string OperatorId, string KeyId, Guid EventId, string Nonce,
    long IssuedAt, long ExpiresAt, PlatformAction Action, PlatformIntent Intent, string Signature);
public sealed record PlatformResult(Guid InstitutionId, Guid? InvitationId, string Status);
public sealed record InviteMember(Guid EventId, Guid RecipientUserId, InstitutionRole[] Roles, string Reason);
public sealed record ChangeInvitation(Guid EventId, Guid InvitationId, string Reason);
public sealed record AcceptInvitation(Guid InstitutionId, Guid InvitationId, Guid EventId, string Challenge)
{public override string ToString()=>"AcceptInvitation [proof redacted]";}
public sealed record InvitationResult(Guid EventId, string Status, Guid? MembershipId = null);
public sealed record InvitationDelivery(Guid InstitutionId, Guid InvitationId, Guid RecipientUserId,
    string Address, string Challenge, DateTime ExpiresAtUtc)
{public override string ToString()=>"InvitationDelivery [recipient and proof redacted]";}

// The provider is installed by the trusted host, never selected by request data.
public interface IInvitationChannel
{
    bool IsOperational { get; }
    Task DeliverAsync(InvitationDelivery delivery, CancellationToken ct);
}
public interface IPlatformAuthority
{
    void Verify(SignedPlatformOperation operation);
    void VerifyCommitted(SignedPlatformOperation operation);
    int ActiveOperatorCount { get; }
}
public interface IRecipientIdentity
{
    Guid VerifySelectionTicket(string ticket);
}
public interface IOnboardingFaultObserver
{
    Task BeforeCommitAsync(string action, CancellationToken ct);
}
public interface IOnboardingRepository
{
    Task<PlatformResult> PlatformAsync(SignedPlatformOperation operation, CancellationToken ct);
    Task<InvitationResult> InviteAsync(TenantContext actor, InviteMember request, CancellationToken ct);
    Task<InvitationResult> ChangeAsync(TenantContext actor, ChangeInvitation request, bool revoke, CancellationToken ct);
    Task<InvitationResult> AcceptAsync(string selectionTicket, AcceptInvitation request, CancellationToken ct);
}
public interface IInstitutionOnboardingManager
{
    Task<PlatformResult> PlatformAsync(SignedPlatformOperation operation, CancellationToken ct = default);
    Task<InvitationResult> InviteAsync(InviteMember request, CancellationToken ct = default);
    Task<InvitationResult> RevokeAsync(ChangeInvitation request, CancellationToken ct = default);
    Task<InvitationResult> ReissueAsync(ChangeInvitation request, CancellationToken ct = default);
    Task<InvitationResult> AcceptAsync(string selectionTicket, AcceptInvitation request, CancellationToken ct = default);
}
