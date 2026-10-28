using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionFormativePlanRepository
{
    Task<IReadOnlyList<FormativePlanInfo>> ListAsync(
        TenantContext actor,
        int page,
        int size,
        CancellationToken ct
    );
    Task<FormativePlanInfo> GetAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<IReadOnlyList<FormativePlanSnapshot>> VersionsAsync(
        TenantContext actor,
        Guid planId,
        int page,
        int size,
        CancellationToken ct
    );
    Task<FormativePlanSnapshot> VersionAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<FormativeHourSummary> ValidateAsync(TenantContext actor, Guid id, CancellationToken ct);
    Task<FormativeReceipt> CreateAsync(TenantContext actor, CreateFormativePlan request, CancellationToken ct);
    Task<FormativeReceipt> RenameAsync(
        TenantContext actor,
        Guid id,
        RenameFormativePlan request,
        CancellationToken ct
    );
    Task<FormativeReceipt> CreateVersionAsync(
        TenantContext actor,
        Guid planId,
        CreateFormativeVersion request,
        CancellationToken ct
    );
    Task<FormativeReceipt> ReplaceAsync(
        TenantContext actor,
        Guid versionId,
        ReplaceFormativeDraft request,
        CancellationToken ct
    );
    Task<FormativeReceipt> PublishAsync(
        TenantContext actor,
        Guid versionId,
        FormativeCommand request,
        CancellationToken ct
    );
    Task<FormativeReceipt> CloneAsync(
        TenantContext actor,
        Guid versionId,
        FormativeCommand request,
        CancellationToken ct
    );
    Task<FormativeReceipt> BindAsync(
        TenantContext actor,
        Guid editionId,
        BindFormativeVersion request,
        CancellationToken ct
    );
    Task<FormativePlanSnapshot> EditionPlanAsync(TenantContext actor, Guid editionId, CancellationToken ct);
    Task<IReadOnlyList<FormativeHistory>> HistoryAsync(
        TenantContext actor,
        Guid planId,
        int page,
        int size,
        CancellationToken ct
    );
}
