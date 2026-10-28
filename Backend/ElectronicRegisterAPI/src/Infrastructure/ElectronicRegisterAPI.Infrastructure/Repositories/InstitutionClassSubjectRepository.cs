using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionClassSubjectRepository(ElectronicRegisterContext db, InstitutionSchoolQueries q, InstitutionAdministrationPersistence audit) : IInstitutionClassSubjectRepository
{
    private IQueryable<InstitutionClassSubjectDto> Project(TenantContext t, IQueryable<E.ClassSubject> rows)
    {
        var events = q.Owned<E.InstitutionAdministrationEvent>(t);
        return rows.Select(r => new InstitutionClassSubjectDto(r.Id, r.ClassId, r.Class.Name, r.SubjectId, r.Subject.Name, r.TeacherId, r.Teacher.FirstName, r.Teacher.LastName, events.Where(e => e.EntityType == "class_subject" && e.EntityId == r.Id).Select(e => (long? )e.Revision).Max() ?? 0L));
    }

    public async Task<InstitutionAcademicPage<InstitutionClassSubjectDto>> ListAsync(
        TenantContext t,
        int page,
        int size,
        Guid? classId,
        Guid? subjectId,
        Guid? teacherId,
        CancellationToken ct
    )
    {
        var rows = q.VisibleAssignments(t);
        if (classId.HasValue)
            rows = rows.Where(r => r.ClassId == classId);
        if (subjectId.HasValue)
            rows = rows.Where(r => r.SubjectId == subjectId);
        if (teacherId.HasValue)
            rows = rows.Where(r => r.TeacherId == teacherId);
        return new(await Project(t, rows.OrderBy(r => r.Class.Name).ThenBy(r => r.Subject.Name).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), await rows.CountAsync(ct), page, size);
    }

    public Task<InstitutionClassSubjectDto?> GetAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        return Project(t, q.Assignments(t).Where(r => r.Id == id)).SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateAsync(TenantContext t, CreateInstitutionClassSubjectDto dto, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await audit.WriteAsync(t, async () =>
        {
            if (!await q.Owned<E.Class>(t).AnyAsync(r => r.Id == dto.ClassId, ct) || !await q.Owned<E.Subject>(t).AnyAsync(r => r.Id == dto.SubjectId, ct) || !await q.Owned<E.Teacher>(t).AnyAsync(r => r.Id == dto.TeacherId, ct))
                throw InstitutionSchoolQueries.Missing();
            q.AddOwned(t, new E.ClassSubject { Id = id, ClassId = dto.ClassId, SubjectId = dto.SubjectId, TeacherId = dto.TeacherId });
            await db.SaveChangesAsync(ct);
            await audit.EventAsync(t, "class_subject", id, 1, "created", null, JsonSerializer.Serialize(dto), ct);
        });
        return id;
    }

    public async Task UpdateAsync(
        TenantContext t,
        Guid id,
        UpdateInstitutionClassSubjectDto dto,
        long revision,
        CancellationToken ct
    )
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "class_subject", id, ct);
            var row = await GetAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            if (row.Revision != revision)
                throw new BusinessRuleException("Risorsa modificata. Ricaricare il dettaglio.");
            if (!await q.Owned<E.Teacher>(t).AnyAsync(r => r.Id == dto.TeacherId, ct))
                throw InstitutionSchoolQueries.Missing();
            await audit.NoDependentsAsync(t, "class_subject", id, ct);
            await q.Owned<E.ClassSubject>(t).Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.TeacherId, dto.TeacherId), ct);
            await audit.EventAsync(t, "class_subject", id, checked(revision + 1), "updated", JsonSerializer.Serialize(row), JsonSerializer.Serialize(new { row.Id, row.ClassId, row.SubjectId, dto.TeacherId }), ct);
        });
    }

    public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await audit.WriteAsync(t, async () =>
        {
            await audit.LockAsync(t, "class_subject", id, ct);
            var row = await GetAsync(t, id, ct) ?? throw InstitutionSchoolQueries.Missing();
            await audit.NoDependentsAsync(t, "class_subject", id, ct);
            await q.Owned<E.ClassSubject>(t).Where(r => r.Id == id).ExecuteDeleteAsync(ct);
            await audit.EventAsync(t, "class_subject", id, checked(row.Revision + 1), "deleted", JsonSerializer.Serialize(row), null, ct);
        });
    }
}
