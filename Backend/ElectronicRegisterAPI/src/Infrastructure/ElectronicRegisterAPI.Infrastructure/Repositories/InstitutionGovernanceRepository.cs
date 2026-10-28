using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Exceptions;
using System.Security.Cryptography;
using System.Text.Json;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class InstitutionOnboardingRepository : IInstitutionGovernanceRepository
{
    private void GovernanceEnabled(bool delivery = false)
    {
        if (!bool.TryParse(config["Multitenancy:Governance:Enabled"], out var enabled) || !enabled || delivery && !channel.IsOperational)
            throw new UnauthorizedAccessException("Governance non operativa.");
    }

    private Task<string?> Status(Guid owner, CancellationToken ct) => Scalar("SELECT status FROM institution WHERE id=@p0", ct, owner);
    private async Task RequireValidAdmin(Guid owner, CancellationToken ct)
    {
        if (await Scalar("SELECT COUNT(*) FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id AND r.role='InstitutionAdmin' JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id JOIN user u ON u.id=m.user_id WHERE m.institution_id=@p0 AND m.status='active' AND s.security_generation>=0", ct, owner) == "0")
            throw new MembershipConflictException();
    }

    public async Task<PlatformResult> ExecuteAsync(SignedPlatformOperation op, CancellationToken ct)
    {
        GovernanceEnabled();
        authority.Verify(op);
        var p = op.Intent;
        Id(p.InstitutionId);
        Text(p.Reason, 500);
        Text(p.EvidenceReference, 200);
        if (op.Action is not (PlatformAction.Suspend or PlatformAction.Reactivate or PlatformAction.Archive or PlatformAction.RequestRecovery or PlatformAction.ApproveRecovery or PlatformAction.RevokeRecovery or PlatformAction.ReissueRecovery))
            throw new ArgumentException();
        var hash = Intent(new { op.OperatorId, op.EventId, op.Action, op.Intent });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Mutex(p.InstitutionId, ct);
        authority.Verify(op);
        if (await Scalar("SELECT COUNT(*) FROM platform_operation_nonce WHERE operator_id=@p0 AND nonce=@p1", ct, op.OperatorId, op.Nonce) != "0")
            throw new UnauthorizedAccessException();
        var prior = await Prior(p.InstitutionId, op.EventId, hash, ct);
        PlatformResult result;
        if (prior is not null)
            result = JsonSerializer.Deserialize<PlatformResult>(prior.Result)!;
        else
        {
            var status = await Status(p.InstitutionId, ct) ?? throw new KeyNotFoundException();
            if (op.Action is PlatformAction.Suspend or PlatformAction.Reactivate or PlatformAction.Archive)
            {
                var next = op.Action switch
                {
                    PlatformAction.Suspend when status == "active" => "suspended",
                    PlatformAction.Reactivate when status == "suspended" => "active",
                    PlatformAction.Archive when status is "active" or "suspended" => "archived",
                    _ => throw new MembershipConflictException()};
                if (next == "active")
                    await RequireValidAdmin(p.InstitutionId, ct);
                await Execute("UPDATE institution SET status=@p0 WHERE id=@p1", ct, next, p.InstitutionId);
                result = new(p.InstitutionId, null, next);
                await Append(p.InstitutionId, op.EventId, "Operator", op.OperatorId, op.KeyId, null, null, "Institution" + op.Action, null, hash, p.Reason, p.EvidenceReference, new { result.InstitutionId, result.InvitationId, result.Status, PreviousStatus = status }, ct);
            // Additional audit context preserves the public result fields for retries.
            }
            else
            {
                if (status != "suspended" || op.Action != PlatformAction.RevokeRecovery && authority.ActiveOperatorCount < 2)
                    throw new UnauthorizedAccessException();
                Id(p.RecipientUserId);
                Id(p.RecoveryId);
                Text(p.LegalIdentity, 200);
                if (await Scalar("SELECT COUNT(*) FROM institution_membership WHERE institution_id=@p0 AND user_id=@p1", ct, p.InstitutionId, p.RecipientUserId) != "0")
                    throw new MembershipConflictException();
                if (op.Action == PlatformAction.RequestRecovery)
                {
                    GovernanceEnabled(true);
                    await Address(p.RecipientUserId, ct);
                    if (await Scalar("SELECT COUNT(*) FROM institution_recovery WHERE institution_id=@p0 AND id=@p1", ct, p.InstitutionId, p.RecoveryId) != "0")
                        throw new MembershipConflictException();
                    // An undelivered initial proof cannot grant access. Delivery is only
                    // permitted after two current independent operator approvals.
                    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    var address = await Address(p.RecipientUserId, ct);
                    await Execute("INSERT INTO institution_invitation(institution_id,id,recipient_user_id,kind,roles,status,token_hash,channel_hash,expires_at_utc,created_at_utc) VALUES(@p0,@p1,@p2,'Recovery',JSON_ARRAY('InstitutionAdmin'),'pending',@p3,@p4,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,UTC_TIMESTAMP(6))", ct, p.InstitutionId, p.RecoveryId, p.RecipientUserId, Hash(token), Hash(address));
                    result = new(p.InstitutionId, p.RecoveryId, "recovery-requested");
                    await Append(p.InstitutionId, op.EventId, "Operator", op.OperatorId, op.KeyId, null, null, "RecoveryRequested", p.RecoveryId, hash, p.Reason, p.EvidenceReference, result, ct);
                    await Execute("INSERT INTO institution_recovery(institution_id,id,recipient_user_id,request_event_id,signed_request,expires_at_utc) VALUES(@p0,@p1,@p2,@p3,@p4,UTC_TIMESTAMP(6)+INTERVAL 1 DAY)", ct, p.InstitutionId, p.RecoveryId, p.RecipientUserId, op.EventId, JsonSerializer.Serialize(op));
                }
                else if (op.Action == PlatformAction.ApproveRecovery)
                {
                    var request = await RecoveryRequest(p.InstitutionId, p.RecoveryId, ct);
                    authority.VerifyCommitted(request);
                    if (request.Intent != p)
                        throw new MembershipConflictException();
                    if (await Scalar("SELECT COUNT(*) FROM institution_recovery_approval WHERE institution_id=@p0 AND recovery_id=@p1 AND operator_id=@p2", ct, p.InstitutionId, p.RecoveryId, op.OperatorId) != "0")
                        throw new MembershipConflictException();
                    var approvals = await RecoveryApprovals(p.InstitutionId, p.RecoveryId, ct);
                    if (approvals.Count >= 2)
                        throw new MembershipConflictException();
                    foreach (var approval in approvals)
                        authority.VerifyCommitted(approval);
                    var invite = await Load(p.InstitutionId, p.RecoveryId, ct);
                    if (invite.Kind != "Recovery" || invite.Status != "pending")
                        throw new MembershipConflictException();
                    if (approvals.Count == 1)
                    {
                        GovernanceEnabled(true);
                        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                        var address = await Address(p.RecipientUserId, ct);
                        var expiry = DateTime.UtcNow.AddMinutes(15);
                        await Execute("UPDATE institution_invitation SET token_hash=@p0,channel_hash=@p1,expires_at_utc=@p2,attempts=0 WHERE institution_id=@p3 AND id=@p4", ct, Hash(token), Hash(address), expiry, p.InstitutionId, p.RecoveryId);
                        await channel.DeliverAsync(new(p.InstitutionId, p.RecoveryId, p.RecipientUserId, address, token, expiry), ct);
                    }

                    result = new(p.InstitutionId, p.RecoveryId, approvals.Count == 1 ? "recovery-approved" : "recovery-awaiting-second-operator");
                    await Append(p.InstitutionId, op.EventId, "Operator", op.OperatorId, op.KeyId, null, null, "RecoveryApproved", p.RecoveryId, hash, p.Reason, p.EvidenceReference, result, ct);
                    await Execute("INSERT INTO institution_recovery_approval(institution_id,recovery_id,operator_id,event_id,signed_operation) VALUES(@p0,@p1,@p2,@p3,@p4)", ct, p.InstitutionId, p.RecoveryId, op.OperatorId, op.EventId, JsonSerializer.Serialize(op));
                }
                else
                {
                    var request = await RecoveryRequest(p.InstitutionId, p.RecoveryId, ct);
                    if ((request.Intent with
                    {
                        Reason = p.Reason,
                        EvidenceReference = p.EvidenceReference
                    }

                    ) != p)
                        throw new MembershipConflictException();
                    var invite = await Load(p.InstitutionId, p.RecoveryId, ct);
                    if (invite.Kind != "Recovery" || invite.Status != "pending")
                        throw new MembershipConflictException();
                    if (op.Action == PlatformAction.RevokeRecovery)
                        await Execute("UPDATE institution_invitation SET status='revoked' WHERE institution_id=@p0 AND id=@p1", ct, p.InstitutionId, p.RecoveryId);
                    else
                    {
                        await AuthorizeRecoveryAcceptance(p.InstitutionId, invite, ct);
                        GovernanceEnabled(true);
                        var proof = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                        var address = await Address(invite.User, ct);
                        var expiry = DateTime.UtcNow.AddMinutes(15);
                        await Execute("UPDATE institution_invitation SET token_hash=@p0,channel_hash=@p1,expires_at_utc=@p2,attempts=0 WHERE institution_id=@p3 AND id=@p4", ct, Hash(proof), Hash(address), expiry, p.InstitutionId, p.RecoveryId);
                        await channel.DeliverAsync(new(p.InstitutionId, p.RecoveryId, invite.User, address, proof, expiry), ct);
                    }

                    result = new(p.InstitutionId, p.RecoveryId, op.Action == PlatformAction.RevokeRecovery ? "recovery-revoked" : "recovery-reissued");
                    await Append(p.InstitutionId, op.EventId, "Operator", op.OperatorId, op.KeyId, null, null, op.Action.ToString(), p.RecoveryId, hash, p.Reason, p.EvidenceReference, result, ct);
                }
            }
        }

        await Execute("INSERT INTO platform_operation_nonce(operator_id,nonce,key_id,institution_id,event_id,expires_at_utc) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)", ct, op.OperatorId, op.Nonce, op.KeyId, p.InstitutionId, op.EventId, DateTimeOffset.FromUnixTimeSeconds(op.ExpiresAt).UtcDateTime);
        await faults.BeforeCommitAsync(op.Action.ToString(), ct);
        authority.Verify(op);
        GovernanceEnabled();
        if (op.Action is PlatformAction.RequestRecovery or PlatformAction.ApproveRecovery or PlatformAction.ReissueRecovery)
        {
            if (authority.ActiveOperatorCount < 2)
                throw new UnauthorizedAccessException();
            authority.VerifyCommitted(await RecoveryRequest(p.InstitutionId, p.RecoveryId, ct));
            foreach (var approval in await RecoveryApprovals(p.InstitutionId, p.RecoveryId, ct))
                authority.VerifyCommitted(approval);
        }

        if (op.Action == PlatformAction.ReissueRecovery)
            await AuthorizeRecoveryAcceptance(p.InstitutionId, await Load(p.InstitutionId, p.RecoveryId, ct), ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<SignedPlatformOperation> RecoveryRequest(Guid owner, Guid id, CancellationToken ct)
    {
        var raw = await Scalar("SELECT signed_request FROM institution_recovery WHERE institution_id=@p0 AND id=@p1 AND expires_at_utc>UTC_TIMESTAMP(6)", ct, owner, id) ?? throw new UnauthorizedAccessException();
        var request = JsonSerializer.Deserialize<SignedPlatformOperation>(raw) ?? throw new UnauthorizedAccessException();
        if (request.Action != PlatformAction.RequestRecovery || request.Intent.InstitutionId != owner || request.Intent.RecoveryId != id)
            throw new UnauthorizedAccessException();
        return request;
    }

    private async Task<List<SignedPlatformOperation>> RecoveryApprovals(Guid owner, Guid id, CancellationToken ct)
    {
        var values = new List<SignedPlatformOperation>();
        await using var cmd = await Command("SELECT signed_operation FROM institution_recovery_approval WHERE institution_id=@p0 AND recovery_id=@p1 ORDER BY operator_id", ct, owner, id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            values.Add(JsonSerializer.Deserialize<SignedPlatformOperation>(reader.GetString(0)) ?? throw new UnauthorizedAccessException());
        return values;
    }

    private async Task AuthorizeRecoveryAcceptance(
        Guid owner,
        Invite invite,
        CancellationToken ct,
        bool beforeMutation = true
    )
    {
        GovernanceEnabled(true);
        if ((await Status(owner, ct)is not ("active" or "suspended")) || beforeMutation && invite.Status == "pending" && await Status(owner, ct) != "suspended" || authority.ActiveOperatorCount < 2)
            throw new UnauthorizedAccessException();
        var request = await RecoveryRequest(owner, invite.Id, ct);
        authority.VerifyCommitted(request);
        var approvals = await RecoveryApprovals(owner, invite.Id, ct);
        if (approvals.Count != 2 || approvals.Select(x => x.OperatorId).Distinct(StringComparer.Ordinal).Count() != 2)
            throw new UnauthorizedAccessException();
        foreach (var op in approvals)
        {
            if (op.Action != PlatformAction.ApproveRecovery || op.Intent != request.Intent)
                throw new UnauthorizedAccessException();
            authority.VerifyCommitted(op);
        }

        if (request.Intent.RecipientUserId != invite.User || invite.Roles.Length != 1 || invite.Roles[0] != "InstitutionAdmin")
            throw new UnauthorizedAccessException();
    }

    private async Task<Guid[]> SuspendPreviousAdministrators(Guid owner, Guid replacement, CancellationToken ct)
    {
        // Replacement is already active with an admin role in this transaction.
        await RequireValidAdmin(owner, ct);
        var prior = new List<Guid>();
        await using (var cmd = await Command("SELECT m.id FROM institution_membership m WHERE m.institution_id=@p0 AND m.id<>@p1 AND m.status='active' AND EXISTS(SELECT 1 FROM institution_membership_role r WHERE r.membership_id=m.id AND r.role='InstitutionAdmin') ORDER BY m.id FOR UPDATE", ct, owner, replacement))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                prior.Add(ReadGuid(reader, 0));
        await Execute("UPDATE institution_membership m SET m.status='suspended' WHERE m.institution_id=@p0 AND m.id<>@p1 AND m.status='active' AND EXISTS(SELECT 1 FROM institution_membership_role r WHERE r.membership_id=m.id AND r.role='InstitutionAdmin')", ct, owner, replacement);
        return prior.ToArray();
    }
}
