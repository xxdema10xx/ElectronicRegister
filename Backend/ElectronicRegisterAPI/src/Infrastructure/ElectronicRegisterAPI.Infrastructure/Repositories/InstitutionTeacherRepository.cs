using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionTeacherRepository(ElectronicRegisterContext db, InstitutionSchoolQueries q) : IInstitutionTeacherRepository
{
    private static IQueryable<Teacher> Project(IQueryable<E.Teacher> rows) => rows.Select(t => new Teacher { Id = t.Id, FirstName = t.FirstName, LastName = t.LastName });
    public Task<List<Teacher>> GetAllAsync(TenantContext t, CancellationToken ct) => Project(q.Owned<E.Teacher>(t)).ToListAsync(ct);
    public Task<int> CountAsync(TenantContext t, CancellationToken ct) => q.Owned<E.Teacher>(t).CountAsync(ct);
    public Task<Teacher?> GetByIdAsync(TenantContext t, Guid id, CancellationToken ct) => Project(q.Owned<E.Teacher>(t).Where(r => r.Id == id)).SingleOrDefaultAsync(ct);
    public Task<List<Teacher>> GetByIdsAsync(TenantContext t, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Project(q.Owned<E.Teacher>(t).Where(r => ids.Contains(r.Id))).ToListAsync(ct);
    public Task<List<Teacher>> GetByLastNameAsync(TenantContext t, string name, CancellationToken ct) => Project(q.Owned<E.Teacher>(t).Where(r => r.LastName == name)).ToListAsync(ct);
    public Task<List<Teacher>> GetForClassAsync(TenantContext t, Guid classId, CancellationToken ct)
    { var assignments = q.VisibleAssignments(t); return Project(q.Owned<E.Teacher>(t)
        .Where(r => assignments.Any(cs => cs.ClassId == classId && cs.TeacherId == r.Id))).ToListAsync(ct); }
    public async Task AddAsync(TenantContext t, Teacher teacher, Guid? membershipId, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        await q.MembershipAsync(t, membershipId, "Teacher", ct);
        q.AddOwned(t, new E.Teacher { Id = teacher.Id, FirstName = teacher.FirstName, LastName = teacher.LastName });
        await InstitutionSchoolQueries.WriteAsync(async () =>
        {
            await db.SaveChangesAsync(ct);
            if (membershipId.HasValue)
            {
                db.InstitutionTeacherProfiles.Add(new() { InstitutionId = t.InstitutionId, MembershipId = membershipId.Value, TeacherId = teacher.Id });
                await db.SaveChangesAsync(ct);
            }
        });
    }
    public async Task UpdateAsync(TenantContext t, Teacher teacher, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        await InstitutionSchoolQueries.WriteAsync(async () =>
        {
            if (await q.Owned<E.Teacher>(t).Where(r => r.Id == teacher.Id).ExecuteUpdateAsync(set =>
                set.SetProperty(r => r.FirstName, teacher.FirstName).SetProperty(r => r.LastName, teacher.LastName), ct) == 0)
                throw InstitutionSchoolQueries.Missing();
        });
    }
    public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        var row = q.Owned<E.Teacher>(t).Where(r => r.Id == id);
        if (!await row.AnyAsync(ct)) throw InstitutionSchoolQueries.Missing();
        if (await q.Assignments(t).AnyAsync(cs => cs.TeacherId == id, ct)) throw new BusinessRuleException("Il docente ha assegnazioni a classi e materie.");
        await InstitutionSchoolQueries.WriteAsync(async () => { if (await row.ExecuteDeleteAsync(ct) == 0) throw InstitutionSchoolQueries.Missing(); });
    }
}
