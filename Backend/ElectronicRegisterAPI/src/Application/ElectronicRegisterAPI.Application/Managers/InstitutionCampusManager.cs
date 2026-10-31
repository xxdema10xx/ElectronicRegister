using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionCampusManager(InstitutionSchoolAuthorization authorization, IInstitutionCampusRepository repository, ITransactionManager transactions) : IInstitutionCampusManager
{
    public async Task<InstitutionAcademicPage<InstitutionCampusDto>> ListAsync(
        int page,
        int size,
        bool? isActive,
        CancellationToken ct
    )
    {
        var t = await authorization.AdminAsync(ct);
        InstitutionClassRules.Page(page, size);
        return await repository.ListAsync(t, page, size, isActive, ct);
    }

    public async Task<InstitutionCampusDto?> GetAsync(Guid id, CancellationToken ct) => await repository.GetAsync(await authorization.AdminAsync(ct), id, ct);
    public async Task<Guid> CreateAsync(CreateInstitutionCampusDto dto, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            Name = InstitutionCampusRules.Name(dto.Name),
            Address = InstitutionCampusRules.Optional(dto.Address, 300),
            City = InstitutionCampusRules.Optional(dto.City, 100),
            Province = InstitutionCampusRules.Optional(dto.Province, 100)
        };
        Guid id = Guid.Empty;
        await transactions.ExecuteAsync(async () => id = await repository.CreateAsync(t, dto, ct), ct);
        return id;
    }

    public async Task UpdateAsync(
        Guid id,
        UpdateInstitutionCampusDto dto,
        long revision,
        CancellationToken ct
    )
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            Name = InstitutionCampusRules.Name(dto.Name),
            Address = InstitutionCampusRules.Optional(dto.Address, 300),
            City = InstitutionCampusRules.Optional(dto.City, 100),
            Province = InstitutionCampusRules.Optional(dto.Province, 100)
        };
        await transactions.ExecuteAsync(() => repository.UpdateAsync(t, id, dto, revision, ct), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        await transactions.ExecuteAsync(() => repository.DeleteAsync(t, id, ct), ct);
    }
}
