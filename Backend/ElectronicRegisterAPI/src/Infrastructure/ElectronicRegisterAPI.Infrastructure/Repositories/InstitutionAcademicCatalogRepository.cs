using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionAcademicCatalogRepository(ElectronicRegisterContext db, InstitutionSchoolQueries q, InstitutionAdministrationPersistence audit) : IInstitutionAcademicCatalogRepository
{
    private IQueryable<InstitutionStudyAreaDto> StudyAreaRows(TenantContext t, IQueryable<E.StudyArea> source)
    {
        var events = q.Owned<E.InstitutionAdministrationEvent>(t);
        return source.Select(r => new InstitutionStudyAreaDto(r.Id, r.Name, r.Description, events.Where(e => e.EntityType == "study_area" && e.EntityId == r.Id).Select(e => (long? )e.Revision).Max() ?? 0L));
    }

    public async Task<InstitutionAcademicPage<InstitutionStudyAreaDto>> ListStudyAreaAsync(
        TenantContext t,
        int page,
        int size,
        CancellationToken ct
    )
    {
        InstitutionSchoolQueries.Admin(t);
        var rows = q.Owned<E.StudyArea>(t);
        return new(await StudyAreaRows(t, rows.OrderBy(r => r.Name).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), await rows.CountAsync(ct), page, size);
    }

    public Task<InstitutionStudyAreaDto?> GetStudyAreaAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        return StudyAreaRows(t, q.Owned<E.StudyArea>(t).Where(r => r.Id == id)).SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateStudyAreaAsync(TenantContext t, CreateInstitutionStudyAreaDto dto, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        var id = Guid.NewGuid();
        await audit.WriteAsync(t, async () =>
        {
            var row = new E.StudyArea
            {
                Id = id,
                Name = dto.Name,
                Description = dto.Description
            };
            q.AddOwned(t, row);
            await db.SaveChangesAsync(ct);
            await audit.EventAsync(t, "study_area", id, 1, "created", null, JsonSerializer.Serialize(dto), ct);
        });
        return id;
    }

    public async Task UpdateStudyAreaAsync(
        TenantContext t,
        Guid id,
        UpdateInstitutionStudyAreaDto dto,
        long revision,
        CancellationToken ct
    )
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "study_area", id, ct);
            var row = await GetStudyAreaAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            if (row.Revision != revision)
                throw new BusinessRuleException("Risorsa modificata. Ricaricare il dettaglio.");
            await q.Owned<E.StudyArea>(t).Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, dto.Name).SetProperty(x => x.Description, dto.Description), ct);
            await audit.EventAsync(t, "study_area", id, checked(revision + 1), "updated", JsonSerializer.Serialize(row), JsonSerializer.Serialize(dto), ct);
        });
    }

    public async Task DeleteStudyAreaAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "study_area", id, ct);
            var row = await GetStudyAreaAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            await audit.NoDependentsAsync(t, "study_area", id, ct);
            await q.Owned<E.StudyArea>(t).Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            await audit.EventAsync(t, "study_area", id, checked(row.Revision + 1), "deleted", JsonSerializer.Serialize(row), null, ct);
        });
    }

    private IQueryable<InstitutionStudyPathDto> StudyPathRows(TenantContext t, IQueryable<E.StudyPath> source)
    {
        var events = q.Owned<E.InstitutionAdministrationEvent>(t);
        return source.Select(r => new InstitutionStudyPathDto(r.Id, r.Name, r.Description, events.Where(e => e.EntityType == "study_path" && e.EntityId == r.Id).Select(e => (long? )e.Revision).Max() ?? 0L));
    }

    public async Task<InstitutionAcademicPage<InstitutionStudyPathDto>> ListStudyPathAsync(
        TenantContext t,
        int page,
        int size,
        CancellationToken ct
    )
    {
        InstitutionSchoolQueries.Admin(t);
        var rows = q.Owned<E.StudyPath>(t);
        return new(await StudyPathRows(t, rows.OrderBy(r => r.Name).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), await rows.CountAsync(ct), page, size);
    }

    public Task<InstitutionStudyPathDto?> GetStudyPathAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        return StudyPathRows(t, q.Owned<E.StudyPath>(t).Where(r => r.Id == id)).SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateStudyPathAsync(TenantContext t, CreateInstitutionStudyPathDto dto, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        var id = Guid.NewGuid();
        await audit.WriteAsync(t, async () =>
        {
            var row = new E.StudyPath
            {
                Id = id,
                Name = dto.Name,
                Description = dto.Description
            };
            q.AddOwned(t, row);
            await db.SaveChangesAsync(ct);
            await audit.EventAsync(t, "study_path", id, 1, "created", null, JsonSerializer.Serialize(dto), ct);
        });
        return id;
    }

    public async Task UpdateStudyPathAsync(
        TenantContext t,
        Guid id,
        UpdateInstitutionStudyPathDto dto,
        long revision,
        CancellationToken ct
    )
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "study_path", id, ct);
            var row = await GetStudyPathAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            if (row.Revision != revision)
                throw new BusinessRuleException("Risorsa modificata. Ricaricare il dettaglio.");
            await q.Owned<E.StudyPath>(t).Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, dto.Name).SetProperty(x => x.Description, dto.Description), ct);
            await audit.EventAsync(t, "study_path", id, checked(revision + 1), "updated", JsonSerializer.Serialize(row), JsonSerializer.Serialize(dto), ct);
        });
    }

    public async Task DeleteStudyPathAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "study_path", id, ct);
            var row = await GetStudyPathAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            await audit.NoDependentsAsync(t, "study_path", id, ct);
            await q.Owned<E.StudyPath>(t).Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            await audit.EventAsync(t, "study_path", id, checked(row.Revision + 1), "deleted", JsonSerializer.Serialize(row), null, ct);
        });
    }

    private IQueryable<InstitutionBienniumDto> BienniumRows(TenantContext t, IQueryable<E.Biennium> source)
    {
        var events = q.Owned<E.InstitutionAdministrationEvent>(t);
        return source.Select(r => new InstitutionBienniumDto(r.Id, r.StartYear, r.EndYear, events.Where(e => e.EntityType == "biennium" && e.EntityId == r.Id).Select(e => (long? )e.Revision).Max() ?? 0L));
    }

    public async Task<InstitutionAcademicPage<InstitutionBienniumDto>> ListBienniumAsync(
        TenantContext t,
        int page,
        int size,
        CancellationToken ct
    )
    {
        InstitutionSchoolQueries.Admin(t);
        var rows = q.Owned<E.Biennium>(t);
        return new(await BienniumRows(t, rows.OrderBy(r => r.StartYear).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), await rows.CountAsync(ct), page, size);
    }

    public Task<InstitutionBienniumDto?> GetBienniumAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        return BienniumRows(t, q.Owned<E.Biennium>(t).Where(r => r.Id == id)).SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateBienniumAsync(TenantContext t, CreateInstitutionBienniumDto dto, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        var id = Guid.NewGuid();
        await audit.WriteAsync(t, async () =>
        {
            var row = new E.Biennium
            {
                Id = id,
                StartYear = dto.StartYear,
                EndYear = dto.EndYear
            };
            q.AddOwned(t, row);
            await db.SaveChangesAsync(ct);
            await audit.EventAsync(t, "biennium", id, 1, "created", null, JsonSerializer.Serialize(dto), ct);
        });
        return id;
    }

    public async Task UpdateBienniumAsync(
        TenantContext t,
        Guid id,
        UpdateInstitutionBienniumDto dto,
        long revision,
        CancellationToken ct
    )
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "biennium", id, ct);
            var row = await GetBienniumAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            if (row.Revision != revision)
                throw new BusinessRuleException("Risorsa modificata. Ricaricare il dettaglio.");
            await q.Owned<E.Biennium>(t).Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.StartYear, dto.StartYear).SetProperty(x => x.EndYear, dto.EndYear), ct);
            await audit.EventAsync(t, "biennium", id, checked(revision + 1), "updated", JsonSerializer.Serialize(row), JsonSerializer.Serialize(dto), ct);
        });
    }

    public async Task DeleteBienniumAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "biennium", id, ct);
            var row = await GetBienniumAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            await audit.NoDependentsAsync(t, "biennium", id, ct);
            await q.Owned<E.Biennium>(t).Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            await audit.EventAsync(t, "biennium", id, checked(row.Revision + 1), "deleted", JsonSerializer.Serialize(row), null, ct);
        });
    }

    private IQueryable<InstitutionBienniumStudyAreaDto> BienniumStudyAreaRows(TenantContext t, IQueryable<E.BienniumStudyArea> source)
    {
        var events = q.Owned<E.InstitutionAdministrationEvent>(t);
        return source.Select(r => new InstitutionBienniumStudyAreaDto(r.Id, r.BienniumId, r.StudyAreaId, events.Where(e => e.EntityType == "biennium_study_area" && e.EntityId == r.Id).Select(e => (long? )e.Revision).Max() ?? 0L));
    }

    public async Task<InstitutionAcademicPage<InstitutionBienniumStudyAreaDto>> ListBienniumStudyAreaAsync(
        TenantContext t,
        int page,
        int size,
        CancellationToken ct
    )
    {
        InstitutionSchoolQueries.Admin(t);
        var rows = q.Owned<E.BienniumStudyArea>(t);
        return new(await BienniumStudyAreaRows(t, rows.OrderBy(r => r.Id).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), await rows.CountAsync(ct), page, size);
    }

    public Task<InstitutionBienniumStudyAreaDto?> GetBienniumStudyAreaAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        return BienniumStudyAreaRows(t, q.Owned<E.BienniumStudyArea>(t).Where(r => r.Id == id)).SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateBienniumStudyAreaAsync(TenantContext t, CreateInstitutionBienniumStudyAreaDto dto, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        var id = Guid.NewGuid();
        await audit.WriteAsync(t, async () =>
        {
            if (!await q.Owned<E.Biennium>(t).AnyAsync(x => x.Id == dto.BienniumId, ct))
                throw InstitutionSchoolQueries.Missing();
            if (!await q.Owned<E.StudyArea>(t).AnyAsync(x => x.Id == dto.StudyAreaId, ct))
                throw InstitutionSchoolQueries.Missing();
            var row = new E.BienniumStudyArea
            {
                Id = id,
                BienniumId = dto.BienniumId,
                StudyAreaId = dto.StudyAreaId
            };
            q.AddOwned(t, row);
            await db.SaveChangesAsync(ct);
            await audit.EventAsync(t, "biennium_study_area", id, 1, "created", null, JsonSerializer.Serialize(dto), ct);
        });
        return id;
    }

    public async Task DeleteBienniumStudyAreaAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "biennium_study_area", id, ct);
            var row = await GetBienniumStudyAreaAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            await audit.NoDependentsAsync(t, "biennium_study_area", id, ct);
            await q.Owned<E.BienniumStudyArea>(t).Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            await audit.EventAsync(t, "biennium_study_area", id, checked(row.Revision + 1), "deleted", JsonSerializer.Serialize(row), null, ct);
        });
    }

    private IQueryable<InstitutionBienniumStudyPathDto> BienniumStudyPathRows(TenantContext t, IQueryable<E.BienniumStudyPath> source)
    {
        var events = q.Owned<E.InstitutionAdministrationEvent>(t);
        return source.Select(r => new InstitutionBienniumStudyPathDto(r.Id, r.BienniumStudyAreaId, r.StudyPathId, events.Where(e => e.EntityType == "biennium_study_path" && e.EntityId == r.Id).Select(e => (long? )e.Revision).Max() ?? 0L));
    }

    public async Task<InstitutionAcademicPage<InstitutionBienniumStudyPathDto>> ListBienniumStudyPathAsync(
        TenantContext t,
        int page,
        int size,
        CancellationToken ct
    )
    {
        InstitutionSchoolQueries.Admin(t);
        var rows = q.Owned<E.BienniumStudyPath>(t);
        return new(await BienniumStudyPathRows(t, rows.OrderBy(r => r.Id).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), await rows.CountAsync(ct), page, size);
    }

    public Task<InstitutionBienniumStudyPathDto?> GetBienniumStudyPathAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        return BienniumStudyPathRows(t, q.Owned<E.BienniumStudyPath>(t).Where(r => r.Id == id)).SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateBienniumStudyPathAsync(TenantContext t, CreateInstitutionBienniumStudyPathDto dto, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        var id = Guid.NewGuid();
        await audit.WriteAsync(t, async () =>
        {
            if (!await q.Owned<E.BienniumStudyArea>(t).AnyAsync(x => x.Id == dto.BienniumStudyAreaId, ct))
                throw InstitutionSchoolQueries.Missing();
            if (!await q.Owned<E.StudyPath>(t).AnyAsync(x => x.Id == dto.StudyPathId, ct))
                throw InstitutionSchoolQueries.Missing();
            var row = new E.BienniumStudyPath
            {
                Id = id,
                BienniumStudyAreaId = dto.BienniumStudyAreaId,
                StudyPathId = dto.StudyPathId
            };
            q.AddOwned(t, row);
            await db.SaveChangesAsync(ct);
            await audit.EventAsync(t, "biennium_study_path", id, 1, "created", null, JsonSerializer.Serialize(dto), ct);
        });
        return id;
    }

    public async Task DeleteBienniumStudyPathAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "biennium_study_path", id, ct);
            var row = await GetBienniumStudyPathAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            await audit.NoDependentsAsync(t, "biennium_study_path", id, ct);
            await q.Owned<E.BienniumStudyPath>(t).Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            await audit.EventAsync(t, "biennium_study_path", id, checked(row.Revision + 1), "deleted", JsonSerializer.Serialize(row), null, ct);
        });
    }
}
