using ElectronicRegisterAPI.Domain.DTOs.Email;
using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionEmailRepository
{
    Task<IReadOnlyList<EmailPolicyVersion>> PoliciesAsync(TenantContext actor, CancellationToken ct);
    Task<EmailPolicyVersion> PolicyAsync(
        TenantContext actor,
        EmailCategory category,
        long? revision,
        CancellationToken ct
    );
    Task<EmailPolicyVersion> WritePolicyAsync(
        TenantContext actor,
        EmailCategory category,
        EmailPolicyWrite request,
        CancellationToken ct
    );
    Task<EmailAssignment> ReserveAsync(TenantContext actor, EmailReserve request, CancellationToken ct);
    Task<EmailAssignment> CreatePersonAsync(TenantContext actor, EmailCreatePerson request, CancellationToken ct);
    Task<EmailAssignment> OverrideAsync(
        TenantContext actor,
        Guid id,
        EmailOverride request,
        CancellationToken ct
    );
    Task<EmailAssignment> RetireAsync(
        TenantContext actor,
        Guid id,
        EmailRetire request,
        CancellationToken ct
    );
    Task<EmailAssignment> AssignmentAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<EmailPage<EmailAssignment>> AssignmentsAsync(
        TenantContext actor,
        int page,
        int size,
        CancellationToken ct
    );
    Task<IReadOnlyList<EmailHistory>> HistoryAsync(
        TenantContext actor,
        Guid? assignment,
        EmailCategory? category,
        int page,
        int size,
        CancellationToken ct
    );
}
