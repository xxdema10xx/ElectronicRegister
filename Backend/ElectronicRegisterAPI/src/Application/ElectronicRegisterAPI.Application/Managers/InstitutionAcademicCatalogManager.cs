using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Application.Managers;
internal sealed class InstitutionAcademicCatalogManager(InstitutionSchoolAuthorization authorization, IInstitutionAcademicCatalogRepository repository, ITransactionManager transactions) : IInstitutionAcademicCatalogManager
{
    public async Task<InstitutionAcademicPage<InstitutionStudyAreaDto>> ListStudyAreaAsync(int page, int size, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        InstitutionClassRules.Page(page, size);
        return await repository.ListStudyAreaAsync(t, page, size, ct);
    }

    public async Task<InstitutionStudyAreaDto?> GetStudyAreaAsync(Guid id, CancellationToken ct) => await repository.GetStudyAreaAsync(await authorization.AdminAsync(ct), id, ct);
    public async Task<Guid> CreateStudyAreaAsync(CreateInstitutionStudyAreaDto dto, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            Name = InstitutionAcademicRules.Name(dto.Name),
            Description = InstitutionAcademicRules.RequiredDescription(dto.Description)
        };
        Guid id = Guid.Empty;
        await transactions.ExecuteAsync(async () => id = await repository.CreateStudyAreaAsync(t, dto, ct), ct);
        return id;
    }

    public async Task UpdateStudyAreaAsync(Guid id, UpdateInstitutionStudyAreaDto dto, long revision, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            Name = InstitutionAcademicRules.Name(dto.Name),
            Description = InstitutionAcademicRules.RequiredDescription(dto.Description)
        };
        await transactions.ExecuteAsync(() => repository.UpdateStudyAreaAsync(t, id, dto, revision, ct), ct);
    }

    public async Task DeleteStudyAreaAsync(Guid id, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        await transactions.ExecuteAsync(() => repository.DeleteStudyAreaAsync(t, id, ct), ct);
    }

    public async Task<InstitutionAcademicPage<InstitutionStudyPathDto>> ListStudyPathAsync(int page, int size, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        InstitutionClassRules.Page(page, size);
        return await repository.ListStudyPathAsync(t, page, size, ct);
    }

    public async Task<InstitutionStudyPathDto?> GetStudyPathAsync(Guid id, CancellationToken ct) => await repository.GetStudyPathAsync(await authorization.AdminAsync(ct), id, ct);
    public async Task<Guid> CreateStudyPathAsync(CreateInstitutionStudyPathDto dto, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            Name = InstitutionAcademicRules.Name(dto.Name),
            Description = InstitutionAcademicRules.Description(dto.Description)
        };
        Guid id = Guid.Empty;
        await transactions.ExecuteAsync(async () => id = await repository.CreateStudyPathAsync(t, dto, ct), ct);
        return id;
    }

    public async Task UpdateStudyPathAsync(Guid id, UpdateInstitutionStudyPathDto dto, long revision, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            Name = InstitutionAcademicRules.Name(dto.Name),
            Description = InstitutionAcademicRules.Description(dto.Description)
        };
        await transactions.ExecuteAsync(() => repository.UpdateStudyPathAsync(t, id, dto, revision, ct), ct);
    }

    public async Task DeleteStudyPathAsync(Guid id, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        await transactions.ExecuteAsync(() => repository.DeleteStudyPathAsync(t, id, ct), ct);
    }

    public async Task<InstitutionAcademicPage<InstitutionBienniumDto>> ListBienniumAsync(int page, int size, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        InstitutionClassRules.Page(page, size);
        return await repository.ListBienniumAsync(t, page, size, ct);
    }

    public async Task<InstitutionBienniumDto?> GetBienniumAsync(Guid id, CancellationToken ct) => await repository.GetBienniumAsync(await authorization.AdminAsync(ct), id, ct);
    public async Task<Guid> CreateBienniumAsync(CreateInstitutionBienniumDto dto, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            StartYear = dto.StartYear,
            EndYear = dto.EndYear
        };
        InstitutionAcademicRules.Dates(dto.StartYear, dto.EndYear);
        Guid id = Guid.Empty;
        await transactions.ExecuteAsync(async () => id = await repository.CreateBienniumAsync(t, dto, ct), ct);
        return id;
    }

    public async Task UpdateBienniumAsync(Guid id, UpdateInstitutionBienniumDto dto, long revision, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            StartYear = dto.StartYear,
            EndYear = dto.EndYear
        };
        InstitutionAcademicRules.Dates(dto.StartYear, dto.EndYear);
        await transactions.ExecuteAsync(() => repository.UpdateBienniumAsync(t, id, dto, revision, ct), ct);
    }

    public async Task DeleteBienniumAsync(Guid id, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        await transactions.ExecuteAsync(() => repository.DeleteBienniumAsync(t, id, ct), ct);
    }

    public async Task<InstitutionAcademicPage<InstitutionBienniumStudyAreaDto>> ListBienniumStudyAreaAsync(int page, int size, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        InstitutionClassRules.Page(page, size);
        return await repository.ListBienniumStudyAreaAsync(t, page, size, ct);
    }

    public async Task<InstitutionBienniumStudyAreaDto?> GetBienniumStudyAreaAsync(Guid id, CancellationToken ct) => await repository.GetBienniumStudyAreaAsync(await authorization.AdminAsync(ct), id, ct);
    public async Task<Guid> CreateBienniumStudyAreaAsync(CreateInstitutionBienniumStudyAreaDto dto, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            BienniumId = InstitutionAcademicRules.Id(dto.BienniumId),
            StudyAreaId = InstitutionAcademicRules.Id(dto.StudyAreaId)
        };
        Guid id = Guid.Empty;
        await transactions.ExecuteAsync(async () => id = await repository.CreateBienniumStudyAreaAsync(t, dto, ct), ct);
        return id;
    }

    public async Task DeleteBienniumStudyAreaAsync(Guid id, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        await transactions.ExecuteAsync(() => repository.DeleteBienniumStudyAreaAsync(t, id, ct), ct);
    }

    public async Task<InstitutionAcademicPage<InstitutionBienniumStudyPathDto>> ListBienniumStudyPathAsync(int page, int size, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        InstitutionClassRules.Page(page, size);
        return await repository.ListBienniumStudyPathAsync(t, page, size, ct);
    }

    public async Task<InstitutionBienniumStudyPathDto?> GetBienniumStudyPathAsync(Guid id, CancellationToken ct) => await repository.GetBienniumStudyPathAsync(await authorization.AdminAsync(ct), id, ct);
    public async Task<Guid> CreateBienniumStudyPathAsync(CreateInstitutionBienniumStudyPathDto dto, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        dto = dto with
        {
            BienniumStudyAreaId = InstitutionAcademicRules.Id(dto.BienniumStudyAreaId),
            StudyPathId = InstitutionAcademicRules.Id(dto.StudyPathId)
        };
        Guid id = Guid.Empty;
        await transactions.ExecuteAsync(async () => id = await repository.CreateBienniumStudyPathAsync(t, dto, ct), ct);
        return id;
    }

    public async Task DeleteBienniumStudyPathAsync(Guid id, CancellationToken ct)
    {
        var t = await authorization.AdminAsync(ct);
        await transactions.ExecuteAsync(() => repository.DeleteBienniumStudyPathAsync(t, id, ct), ct);
    }
}
