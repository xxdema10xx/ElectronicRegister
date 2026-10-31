using ElectronicRegisterAPI.Domain.DTOs.Email;
using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

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
    Task<IReadOnlyList<EmailHistory>> HistoryAsync(
        Guid? assignment,
        EmailCategory? category,
        int page,
        int size,
        CancellationToken ct
    );
}
