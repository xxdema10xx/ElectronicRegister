using System.Text.Json.Serialization;
namespace ElectronicRegisterAPI.Domain.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstitutionEmailFeature(bool Enabled);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailPolicyVersion(Guid PolicyId, EmailCategory Category, long Revision,
    string State, EmailTemplate Template, Guid ActorUserId, Guid ActorMembershipId, DateTime CreatedAtUtc,
    string DomainVerificationState = "ConfiguredUnverified");
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailRecipient(Guid? StudentId, Guid? TeacherId, Guid? MembershipId,
    string? FirstName = null, string? LastName = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailAssignment(Guid Id, Guid? StudentId, Guid? TeacherId, Guid? MembershipId,
    EmailCategory Category, long PolicyRevision, string Email, string State, long Revision,
    string FirstNameSnapshot, string LastNameSnapshot, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    bool MailboxVerified = false, bool DomainVerified = false, bool LoginEnabled = false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailPolicyWrite(Guid EventId, long ExpectedRevision, string State, EmailTemplate Template, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailReserve(Guid EventId, EmailCategory Category, long PolicyRevision, EmailRecipient Recipient);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailCreatePerson(Guid EventId, EmailCategory Category, long PolicyRevision,
    string FirstName, string LastName, Guid? ClassId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailOverride(Guid EventId, long ExpectedRevision, long PolicyRevision, string Email, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailRetire(Guid EventId, long ExpectedRevision, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailPreviewRequest(EmailCategory Category, long? PolicyRevision, string? FirstName, string? LastName, EmailTemplate? Draft = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailPage<T>(int PageNumber, int PageSize, long TotalCount, IReadOnlyList<T> Items);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailHistory(Guid EventId, string Operation, Guid ActorUserId, Guid ActorMembershipId,
    string? BeforeJson, string AfterJson, string Reason, DateTime OccurredAtUtc);

public interface IInstitutionEmailRepository
{
    Task<IReadOnlyList<EmailPolicyVersion>> PoliciesAsync(TenantContext actor, CancellationToken ct);
    Task<EmailPolicyVersion> PolicyAsync(TenantContext actor, EmailCategory category, long? revision, CancellationToken ct);
    Task<EmailPolicyVersion> WritePolicyAsync(TenantContext actor, EmailCategory category, EmailPolicyWrite request, CancellationToken ct);
    Task<EmailAssignment> ReserveAsync(TenantContext actor, EmailReserve request, CancellationToken ct);
    Task<EmailAssignment> CreatePersonAsync(TenantContext actor, EmailCreatePerson request, CancellationToken ct);
    Task<EmailAssignment> OverrideAsync(TenantContext actor, Guid id, EmailOverride request, CancellationToken ct);
    Task<EmailAssignment> RetireAsync(TenantContext actor, Guid id, EmailRetire request, CancellationToken ct);
    Task<EmailAssignment> AssignmentAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<EmailPage<EmailAssignment>> AssignmentsAsync(TenantContext actor, int page, int size, CancellationToken ct);
    Task<IReadOnlyList<EmailHistory>> HistoryAsync(TenantContext actor, Guid? assignment, EmailCategory? category, int page, int size, CancellationToken ct);
}
public interface IInstitutionEmailManager
{
    Task<IReadOnlyList<EmailPolicyVersion>> PoliciesAsync(CancellationToken ct);
    Task<EmailPolicyVersion> PolicyAsync(EmailCategory category, long? revision, CancellationToken ct);
    Task<EmailPolicyVersion> WritePolicyAsync(EmailCategory category, EmailPolicyWrite request, CancellationToken ct);
    Task<GeneratedInstitutionEmail> PreviewAsync(EmailPreviewRequest request, CancellationToken ct);
    Task<EmailAssignment> ReserveAsync(EmailReserve request, CancellationToken ct);
    Task<EmailAssignment> CreatePersonAsync(EmailCreatePerson request, CancellationToken ct);
    Task<EmailAssignment> OverrideAsync(Guid id, EmailOverride request, CancellationToken ct);
    Task<EmailAssignment> RetireAsync(Guid id, EmailRetire request, CancellationToken ct);
    Task<EmailAssignment> AssignmentAsync(Guid id, CancellationToken ct);
    Task<EmailPage<EmailAssignment>> AssignmentsAsync(int page, int size, CancellationToken ct);
    Task<IReadOnlyList<EmailHistory>> HistoryAsync(Guid? assignment, EmailCategory? category, int page, int size, CancellationToken ct);
}
