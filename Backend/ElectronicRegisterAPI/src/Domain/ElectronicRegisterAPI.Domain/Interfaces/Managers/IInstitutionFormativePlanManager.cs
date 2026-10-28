using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionFormativePlanManager
{
    Task<IReadOnlyList<FormativePlanInfo>> ListAsync(int page, int size, CancellationToken ct);
    Task<FormativePlanInfo> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<FormativePlanSnapshot>> VersionsAsync(
        Guid planId,
        int page,
        int size,
        CancellationToken ct
    );
    Task<FormativePlanSnapshot> VersionAsync(Guid id, CancellationToken ct);
    Task<FormativeHourSummary> ValidateAsync(Guid id, CancellationToken ct);
    Task<FormativeReceipt> CreateAsync(CreateFormativePlan request, CancellationToken ct);
    Task<FormativeReceipt> RenameAsync(Guid id, RenameFormativePlan request, CancellationToken ct);
    Task<FormativeReceipt> CreateVersionAsync(Guid planId, CreateFormativeVersion request, CancellationToken ct);
    Task<FormativeReceipt> ReplaceAsync(Guid versionId, ReplaceFormativeDraft request, CancellationToken ct);
    Task<FormativeReceipt> PublishAsync(Guid versionId, FormativeCommand request, CancellationToken ct);
    Task<FormativeReceipt> CloneAsync(Guid versionId, FormativeCommand request, CancellationToken ct);
    Task<FormativeReceipt> BindAsync(Guid editionId, BindFormativeVersion request, CancellationToken ct);
    Task<FormativePlanSnapshot> EditionPlanAsync(Guid editionId, CancellationToken ct);
    Task<IReadOnlyList<FormativeHistory>> HistoryAsync(
        Guid planId,
        int page,
        int size,
        CancellationToken ct
    );
}
