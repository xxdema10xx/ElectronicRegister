using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionFormativePlanManager(InstitutionSchoolAuthorization authorization, IInstitutionFormativePlanRepository repository, FormativePlanFeature feature) : IInstitutionFormativePlanManager
{
    private async Task<TenantContext> Actor(CancellationToken ct, bool admin = false, bool catalog = false)
    {
        if (!feature.Enabled)
            throw new UnauthorizedAccessException();
        var t = await authorization.RequireAsync(ct);
        if (admin && !t.Roles.Contains(InstitutionRole.InstitutionAdmin))
            throw new UnauthorizedAccessException();
        if (catalog && !t.Roles.Contains(InstitutionRole.InstitutionAdmin) && !t.Roles.Contains(InstitutionRole.Staff))
            throw new UnauthorizedAccessException();
        return t;
    }

    public async Task<IReadOnlyList<FormativePlanInfo>> ListAsync(int page, int size, CancellationToken ct) => await repository.ListAsync(await Actor(ct, catalog: true), page, size, ct);
    public async Task<FormativePlanInfo> GetAsync(Guid id, CancellationToken ct) => await repository.GetAsync(await Actor(ct, catalog: true), id, ct);
    public async Task<IReadOnlyList<FormativePlanSnapshot>> VersionsAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct
    ) => await repository.VersionsAsync(await Actor(ct, catalog: true), id, page, size, ct);
    public async Task<FormativePlanSnapshot> VersionAsync(Guid id, CancellationToken ct) => await repository.VersionAsync(await Actor(ct, catalog: true), id, ct);
    public async Task<FormativeHourSummary> ValidateAsync(Guid id, CancellationToken ct) => await repository.ValidateAsync(await Actor(ct, catalog: true), id, ct);
    public async Task<FormativeReceipt> CreateAsync(CreateFormativePlan r, CancellationToken ct) => await repository.CreateAsync(await Actor(ct, admin: true), r, ct);
    public async Task<FormativeReceipt> RenameAsync(Guid id, RenameFormativePlan r, CancellationToken ct) => await repository.RenameAsync(await Actor(ct, admin: true), id, r, ct);
    public async Task<FormativeReceipt> CreateVersionAsync(Guid id, CreateFormativeVersion r, CancellationToken ct) => await repository.CreateVersionAsync(await Actor(ct, admin: true), id, r, ct);
    public async Task<FormativeReceipt> ReplaceAsync(Guid id, ReplaceFormativeDraft r, CancellationToken ct) => await repository.ReplaceAsync(await Actor(ct, admin: true), id, r, ct);
    public async Task<FormativeReceipt> PublishAsync(Guid id, FormativeCommand r, CancellationToken ct) => await repository.PublishAsync(await Actor(ct, admin: true), id, r, ct);
    public async Task<FormativeReceipt> CloneAsync(Guid id, FormativeCommand r, CancellationToken ct) => await repository.CloneAsync(await Actor(ct, admin: true), id, r, ct);
    public async Task<FormativeReceipt> BindAsync(Guid id, BindFormativeVersion r, CancellationToken ct) => await repository.BindAsync(await Actor(ct, admin: true), id, r, ct);
    public async Task<FormativePlanSnapshot> EditionPlanAsync(Guid id, CancellationToken ct) => await repository.EditionPlanAsync(await Actor(ct), id, ct);
    public async Task<IReadOnlyList<FormativeHistory>> HistoryAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct
    ) => await repository.HistoryAsync(await Actor(ct, admin: true), id, page, size, ct);
}
