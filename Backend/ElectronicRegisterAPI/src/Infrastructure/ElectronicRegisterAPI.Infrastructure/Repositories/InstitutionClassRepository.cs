using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Microsoft.EntityFrameworkCore.Storage;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionClassRepository(ElectronicRegisterContext db, InstitutionSchoolQueries q) : IInstitutionClassRepository
{
    private IQueryable<E.InstitutionClassEvent> Events(TenantContext t) => q.Owned<E.InstitutionClassEvent>(t);
    private IQueryable<InstitutionClassDto> Project(TenantContext t, IQueryable<E.Class> rows)
    {
        var events = Events(t);
        return rows.Select(c =>
        new InstitutionClassDto(c.Id, c.Name, c.BienniumStudyPathId, EF.Property<Guid?>(c, "CourseEditionId"),
            events.Where(e => e.ClassId == c.Id).Select(e => (long?)e.Revision).Max() ?? 0L));
    }

    public async Task<InstitutionClassPageDto> GetAllAsync(TenantContext t, int page, int size, Guid? path, Guid? edition, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        var rows = q.Owned<E.Class>(t);
        if (path.HasValue) rows = rows.Where(c => c.BienniumStudyPathId == path);
        if (edition.HasValue) rows = rows.Where(c => EF.Property<Guid?>(c, "CourseEditionId") == edition);
        var count = await rows.CountAsync(ct);
        var items = await Project(t, rows.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new(items, count, page, size);
    }
    public Task<InstitutionClassDto?> GetByIdAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        return Project(t, q.Owned<E.Class>(t).Where(c => c.Id == id)).SingleOrDefaultAsync(ct);
    }
    private async Task<E.Class> LockAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Class mutation requires a transaction.");
        return await db.Classes.FromSqlInterpolated($"SELECT * FROM class WHERE institution_id={t.InstitutionId} AND id={id} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw InstitutionSchoolQueries.Missing();
    }
    private async Task<long> RevisionAsync(TenantContext t, Guid id, CancellationToken ct) =>
        await Events(t).Where(e => e.ClassId == id).Select(e => (long?)e.Revision).MaxAsync(ct) ?? 0L;
    private static string Snapshot(E.Class row, Guid? edition) => JsonSerializer.Serialize(new
        { row.Id, row.Name, row.BienniumStudyPathId, CourseEditionId = edition });
    private Task AuditAsync(TenantContext t, Guid id, long revision, string action, string? before, string? after, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO institution_class_event (institution_id,id,actor_user_id,actor_membership_id,class_id,revision,action,before_json,after_json,occurred_at_utc) VALUES ({t.InstitutionId},{Guid.NewGuid()},{t.UserId},{t.MembershipId},{id},{revision},{action},{before},{after},UTC_TIMESTAMP(6))", ct);
    private static async Task WriteAsync(Func<Task> write)
    {
        try { await write(); }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 or 1451 or 1452 or 1644 or 1213 or 1205 })
        { throw new BusinessRuleException("Classe gia presente, modificata o utilizzata."); }
        catch (MySqlException e) when (e.Number is 1062 or 1451 or 1452 or 1644 or 1213 or 1205)
        { throw new BusinessRuleException("Classe gia presente, modificata o utilizzata."); }
    }
    public async Task AddAsync(TenantContext t, Guid id, string name, Guid path, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Class mutation requires a transaction.");
        if (!await q.Owned<E.BienniumStudyPath>(t).AnyAsync(p => p.Id == path, ct)) throw InstitutionSchoolQueries.Missing();
        await WriteAsync(async () =>
        {
            var row = new E.Class { Id = id, Name = name, BienniumStudyPathId = path };
            q.AddOwned(t, row);
            await db.SaveChangesAsync(ct); // SQL UNIQUE is authoritative for collation and concurrent inserts.
            await AuditAsync(t, id, 1, "class.created", null, Snapshot(row, null), ct);
        });
    }
    public async Task UpdateAsync(TenantContext t, Guid id, string name, long expectedRevision, CancellationToken ct)
    {
        await WriteAsync(async () =>
        {
            var row = await LockAsync(t, id, ct);
            var revision = await RevisionAsync(t, id, ct);
            if (revision != expectedRevision) throw new BusinessRuleException("La classe e stata modificata. Ricaricare il dettaglio.");
            // Read the shadow value through a SQL projection; AsNoTracking does not retain shadow state.
            var edition = await q.Owned<E.Class>(t).Where(c => c.Id == id).Select(c => EF.Property<Guid?>(c, "CourseEditionId")).SingleAsync(ct);
            var before = Snapshot(row, edition);
            var changed = await q.Owned<E.Class>(t).Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, name), ct);
            if (changed != 1) throw InstitutionSchoolQueries.Missing();
            row.Name = name;
            await AuditAsync(t, id, checked(revision + 1), "class.renamed", before, Snapshot(row, edition), ct);
        });
    }
    public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        await WriteAsync(async () =>
        {
            var row = await LockAsync(t, id, ct);
            var edition = await q.Owned<E.Class>(t).Where(c => c.Id == id).Select(c => EF.Property<Guid?>(c, "CourseEditionId")).SingleAsync(ct);
            if (edition.HasValue || await q.Owned<E.Student>(t).AnyAsync(s => s.ClassId == id, ct) ||
                await q.Owned<E.ClassSubject>(t).AnyAsync(s => s.ClassId == id, ct) ||
                await db.CourseAdministrationEvents.AnyAsync(e => e.InstitutionId == t.InstitutionId && e.ClassId == id, ct))
                throw new BusinessRuleException("La classe contiene riferimenti didattici o storici.");
            // Fail closed if a future schema adds a FK (especially CASCADE/SET NULL) not inventoried here.
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "SELECT DISTINCT TABLE_NAME,CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME='class'";
            var known = new HashSet<string> { "student:fk_student_class", "student:fk_mt3a_student_class", "class_subject:fk_cs_class", "class_subject:fk_mt3a_cs_class", "course_administration_event:fk_mt4c_class", "student_class_transfer_event:fk_transfer_from_class", "student_class_transfer_event:fk_transfer_to_class" };
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct))
                    if (!known.Remove(reader.GetString(0) + ":" + reader.GetString(1))) throw new BusinessRuleException("Inventario dipendenze classe da aggiornare.");
            if (known.Count != 0) throw new BusinessRuleException("Schema delle dipendenze classe non valido.");
            var revision = await RevisionAsync(t, id, ct);
            await q.Owned<E.Class>(t).Where(c => c.Id == id).ExecuteDeleteAsync(ct);
            await AuditAsync(t, id, checked(revision + 1), "class.deleted", Snapshot(row, edition), null, ct);
        });
    }
}
