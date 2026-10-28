using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionCampusRepository(ElectronicRegisterContext db, InstitutionSchoolQueries q, InstitutionAdministrationPersistence audit) : IInstitutionCampusRepository
{
    private IQueryable<InstitutionCampusDto> Project(TenantContext t, IQueryable<E.Campus> rows)
    {
        var events = q.Owned<E.InstitutionAdministrationEvent>(t);
        return rows.Select(r => new InstitutionCampusDto(r.Id, r.Name, r.Address, r.City, r.Province, r.IsActive ?? true, events.Where(e => e.EntityType == "campus" && e.EntityId == r.Id).Select(e => (long? )e.Revision).Max() ?? 0L));
    }

    public async Task<InstitutionAcademicPage<InstitutionCampusDto>> ListAsync(
        TenantContext t,
        int page,
        int size,
        bool? isActive,
        CancellationToken ct
    )
    {
        InstitutionSchoolQueries.Admin(t);
        var rows = q.Owned<E.Campus>(t);
        if (isActive.HasValue)
            rows = rows.Where(r => r.IsActive == isActive);
        return new(await Project(t, rows.OrderBy(r => r.Name).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), await rows.CountAsync(ct), page, size);
    }

    public Task<InstitutionCampusDto?> GetAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        return Project(t, q.Owned<E.Campus>(t).Where(r => r.Id == id)).SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateAsync(TenantContext t, CreateInstitutionCampusDto dto, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await audit.WriteAsync(t, async () =>
        {
            q.AddOwned(t, new E.Campus { Id = id, Name = dto.Name, Address = dto.Address, City = dto.City, Province = dto.Province, IsActive = dto.IsActive });
            await db.SaveChangesAsync(ct);
            await audit.EventAsync(t, "campus", id, 1, "created", null, JsonSerializer.Serialize(dto), ct);
        });
        return id;
    }

    public async Task UpdateAsync(
        TenantContext t,
        Guid id,
        UpdateInstitutionCampusDto dto,
        long revision,
        CancellationToken ct
    )
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "campus", id, ct);
            var row = await GetAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            if (row.Revision != revision)
                throw new BusinessRuleException("Risorsa modificata. Ricaricare il dettaglio.");
            await q.Owned<E.Campus>(t).Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.Name, dto.Name).SetProperty(r => r.Address, dto.Address).SetProperty(r => r.City, dto.City).SetProperty(r => r.Province, dto.Province).SetProperty(r => r.IsActive, dto.IsActive), ct);
            await audit.EventAsync(t, "campus", id, checked(revision + 1), "updated", JsonSerializer.Serialize(row), JsonSerializer.Serialize(dto), ct);
        });
    }

    public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "campus", id, ct);
            var row = await GetAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            await audit.NoDependentsAsync(t, "campus", id, ct);
            await q.Owned<E.Campus>(t).Where(r => r.Id == id).ExecuteDeleteAsync(ct);
            await audit.EventAsync(t, "campus", id, checked(row.Revision + 1), "deleted", JsonSerializer.Serialize(row), null, ct);
        });
    }
}
