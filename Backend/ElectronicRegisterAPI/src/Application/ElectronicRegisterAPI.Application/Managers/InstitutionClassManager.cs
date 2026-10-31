using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionClassManager(InstitutionSchoolAuthorization authorization, IInstitutionClassRepository classes, ITransactionManager transactions) : IInstitutionClassManager
{
    public async Task<InstitutionClassPageDto> GetAllAsync(
        int page,
        int size,
        Guid? path,
        Guid? edition,
        CancellationToken ct
    )
    {
        var tenant = await authorization.AdminAsync(ct);
        InstitutionClassRules.Page(page, size);
        if (path == Guid.Empty || edition == Guid.Empty)
            throw new ArgumentException("Filtro non valido.");
        return await classes.GetAllAsync(tenant, page, size, path, edition, ct);
    }

    public async Task<InstitutionClassDto?> GetByIdAsync(Guid id, CancellationToken ct) => await classes.GetByIdAsync(await authorization.AdminAsync(ct), id, ct);
    public async Task<Guid> AddAsync(CreateInstitutionClassDto dto, CancellationToken ct)
    {
        var tenant = await authorization.AdminAsync(ct);
        var name = InstitutionClassRules.Name(dto.Name);
        if (dto.BienniumStudyPathId == Guid.Empty)
            throw new ArgumentException("Percorso non valido.");
        var id = Guid.NewGuid();
        await transactions.ExecuteAsync(() => classes.AddAsync(tenant, id, name, dto.BienniumStudyPathId, ct), ct);
        return id;
    }

    public async Task UpdateAsync(
        Guid id,
        UpdateInstitutionClassDto dto,
        long expectedRevision,
        CancellationToken ct
    )
    {
        var tenant = await authorization.AdminAsync(ct);
        var name = InstitutionClassRules.Name(dto.Name);
        if (expectedRevision < 0)
            throw new ArgumentException("Revisione non valida.");
        await transactions.ExecuteAsync(() => classes.UpdateAsync(tenant, id, name, expectedRevision, ct), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var tenant = await authorization.AdminAsync(ct);
        await transactions.ExecuteAsync(() => classes.DeleteAsync(tenant, id, ct), ct);
    }
}
