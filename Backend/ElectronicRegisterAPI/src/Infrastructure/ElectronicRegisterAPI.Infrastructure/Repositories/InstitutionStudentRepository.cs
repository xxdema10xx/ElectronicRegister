using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionStudentRepository(ElectronicRegisterContext db, InstitutionSchoolQueries q) : IInstitutionStudentRepository
{
    private static IQueryable<Student> Project(IQueryable<E.Student> rows) => rows.Select(s => new Student
        { Id = s.Id, FirstName = s.FirstName, LastName = s.LastName, ClassId = s.ClassId });
    public Task<List<Student>> GetAllAsync(TenantContext t, CancellationToken ct) => Project(q.VisibleStudents(t)).ToListAsync(ct);
    public async Task<List<Student>> GetForClassAsync(TenantContext t, Guid classId, CancellationToken ct)
    {
        if (!await q.Owned<E.Class>(t).AnyAsync(c => c.Id == classId &&
            (t.Roles.Contains(Domain.Enums.InstitutionRole.InstitutionAdmin) || q.VisibleAssignments(t).Any(cs => cs.ClassId == c.Id)), ct))
            throw InstitutionSchoolQueries.Missing();
        return await Project(q.VisibleStudents(t).Where(s => s.ClassId == classId)).ToListAsync(ct);
    }
    public Task<int> CountAsync(TenantContext t, CancellationToken ct) => q.VisibleStudents(t).CountAsync(ct);
    public Task<Student?> GetByIdAsync(TenantContext t, Guid id, CancellationToken ct) => Project(q.VisibleStudents(t).Where(s => s.Id == id)).SingleOrDefaultAsync(ct);
    public Task<List<Student>> GetByIdsAsync(TenantContext t, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Project(q.VisibleStudents(t).Where(s => ids.Contains(s.Id))).ToListAsync(ct);
    public Task<List<Student>> GetByLastNameAsync(TenantContext t, string name, CancellationToken ct) => Project(q.VisibleStudents(t).Where(s => s.LastName == name)).ToListAsync(ct);
    public Task<Student?> GetOwnAsync(TenantContext t, Guid id, CancellationToken ct)
    { var profiles = q.StudentProfiles(t); return Project(q.Owned<E.Student>(t).Where(s => s.Id == id && profiles.Any(p => p.StudentId == s.Id))).SingleOrDefaultAsync(ct); }

    public async Task AddAsync(TenantContext t, Student student, Guid? membershipId, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        if (student.ClassId.HasValue && !await q.Owned<E.Class>(t).AnyAsync(c => c.Id == student.ClassId, ct)) throw InstitutionSchoolQueries.Missing();
        await q.MembershipAsync(t, membershipId, "Student", ct);
        q.AddOwned(t, new E.Student { Id = student.Id, FirstName = student.FirstName, LastName = student.LastName, ClassId = student.ClassId });
        await InstitutionSchoolQueries.WriteAsync(async () =>
        {
            await db.SaveChangesAsync(ct);
            if (membershipId.HasValue)
            {
                db.InstitutionStudentProfiles.Add(new() { InstitutionId = t.InstitutionId, MembershipId = membershipId.Value, StudentId = student.Id });
                await db.SaveChangesAsync(ct);
            }
        });
    }
    public async Task UpdateAsync(TenantContext t, Student student, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        await InstitutionSchoolQueries.WriteAsync(async () =>
        {
            // Class and Institution are intentionally not updateable through this name-edit contract.
            if (await q.Owned<E.Student>(t).Where(s => s.Id == student.Id).ExecuteUpdateAsync(set =>
                set.SetProperty(s => s.FirstName, student.FirstName).SetProperty(s => s.LastName, student.LastName), ct) == 0)
                throw InstitutionSchoolQueries.Missing();
        });
    }
    public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La cancellazione richiede una transazione applicativa.");
        // Current reads, scoped at SQL source. The parent X-lock serializes FK
        // inserts for this one student; it never locks an institution or Exam.
        var parents = await db.Students.FromSqlInterpolated($"SELECT * FROM student WHERE institution_id={t.InstitutionId} AND id={id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (parents.Count == 0) throw InstitutionSchoolQueries.Missing();
        // The manager starts this transaction after authorization. Parent current
        // read is its first statement, so this first consistent read sees grades
        // committed before the lock; FK inserts cannot pass the held parent lock.
        // No grade-range/gap lock or loading of grade values is necessary.
        if (await q.Owned<E.Grade>(t).AnyAsync(g => g.StudentId == id, ct)) throw new BusinessRuleException("Lo studente ha voti registrati.");
        var row = q.Owned<E.Student>(t).Where(s => s.Id == id);
        await InstitutionSchoolQueries.WriteAsync(async () =>
        {
            // Recheck in the DELETE statement: a grade committed after the first
            // check must not be cascade-deleted by the historical student FK.
            var grades = q.Owned<E.Grade>(t);
            if (await row.Where(s => !grades.Any(g => g.StudentId == s.Id)).ExecuteDeleteAsync(ct) == 0)
                throw new BusinessRuleException("Studente non eliminabile.");
        });
    }
}
