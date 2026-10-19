using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionSubjectRepository(ElectronicRegisterContext db, InstitutionSchoolQueries q) : IInstitutionSubjectRepository
{
    private static IQueryable<Subject> Project(IQueryable<E.Subject> rows) => rows.Select(s => new Subject { Id = s.Id, Name = s.Name });
    public Task<List<Subject>> GetAllAsync(TenantContext t, CancellationToken ct) => Project(q.VisibleSubjects(t)).ToListAsync(ct);
    public Task<int> CountAsync(TenantContext t, CancellationToken ct) => q.VisibleSubjects(t).CountAsync(ct);
    public Task<Subject?> GetByIdAsync(TenantContext t, Guid id, CancellationToken ct) => Project(q.VisibleSubjects(t).Where(s => s.Id == id)).SingleOrDefaultAsync(ct);
    public Task<List<Subject>> GetByIdsAsync(TenantContext t, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Project(q.VisibleSubjects(t).Where(s => ids.Contains(s.Id))).ToListAsync(ct);
    public Task<Subject?> GetByNameAsync(TenantContext t, string name, CancellationToken ct) => Project(q.VisibleSubjects(t).Where(s => s.Name == name)).SingleOrDefaultAsync(ct);
    public Task<List<Subject>> GetByTeacherAsync(TenantContext t, Guid teacherId, CancellationToken ct)
    { var assignments = q.VisibleAssignments(t); return Project(q.Owned<E.Subject>(t)
        .Where(s => assignments.Any(cs => cs.TeacherId == teacherId && cs.SubjectId == s.Id))).ToListAsync(ct); }
    private async Task NameAvailable(TenantContext t, Subject subject, CancellationToken ct)
    {
        if (await q.Owned<E.Subject>(t).AnyAsync(s => s.Name == subject.Name && s.Id != subject.Id, ct)) throw new BusinessRuleException("Nome materia gia in uso nell'istituto.");
    }
    public async Task AddAsync(TenantContext t, Subject subject, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        await NameAvailable(t, subject, ct);
        q.AddOwned(t, new E.Subject { Id = subject.Id, Name = subject.Name });
        await InstitutionSchoolQueries.WriteAsync(async () => { await db.SaveChangesAsync(ct); });
    }
    public async Task UpdateAsync(TenantContext t, Subject subject, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        if (!await q.Owned<E.Subject>(t).AnyAsync(s => s.Id == subject.Id, ct)) throw InstitutionSchoolQueries.Missing();
        await NameAvailable(t, subject, ct);
        await InstitutionSchoolQueries.WriteAsync(async () =>
        {
            if (await q.Owned<E.Subject>(t).Where(s => s.Id == subject.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.Name, subject.Name), ct) == 0)
                throw InstitutionSchoolQueries.Missing();
        });
    }
    public async Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(t);
        var row = q.Owned<E.Subject>(t).Where(s => s.Id == id);
        if (!await row.AnyAsync(ct)) throw InstitutionSchoolQueries.Missing();
        if (await q.Assignments(t).AnyAsync(cs => cs.SubjectId == id, ct)) throw new BusinessRuleException("La materia e assegnata a classi.");
        await InstitutionSchoolQueries.WriteAsync(async () => { if (await row.ExecuteDeleteAsync(ct) == 0) throw InstitutionSchoolQueries.Missing(); });
    }
}
