using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionSchoolQueries(ElectronicRegisterContext db,
    Domain.Interfaces.Repositories.IInstitutionWriteAuthorization authorization)
{
    internal async Task WriteAuthorizedAsync(TenantContext tenant, InstitutionPermission permission, Func<Task> write, CancellationToken ct)
    {
        InstitutionPermissions.Require(tenant, permission);
        await using var owned = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await authorization.LockAsync(tenant, ct);
        await write();
        if (owned is not null) await owned.CommitAsync(ct);
    }
    internal IQueryable<T> Owned<T>(TenantContext tenant) where T : class
    {
        var rows = db.Set<T>().AsNoTracking().Where(e => EF.Property<Guid>(e, "InstitutionId") == tenant.InstitutionId);
        if (!tenant.Roles.Contains(InstitutionRole.Staff)) return rows;
        var roles = tenant.Roles.Select(r => r.ToString()).ToArray();
        // Staff reads also bind membership/generation at the SQL source. Direct
        // repository calls cannot turn a stale or fabricated Staff context into a directory.
        return rows.Where(_ => db.InstitutionMemberships.Any(m => m.Id == tenant.MembershipId &&
            m.InstitutionId == tenant.InstitutionId && m.UserId == tenant.UserId && m.User.Id == tenant.UserId &&
            m.Status == "active" && m.Institution.Status == "active" &&
            m.InstitutionMembershipRoles.Count == roles.Length && m.InstitutionMembershipRoles.All(r => roles.Contains(r.Role)) &&
            db.InstitutionMembershipSecurityStates.Any(s => s.InstitutionId == tenant.InstitutionId && s.MembershipId == m.Id &&
                s.SecurityGeneration >= 0 && s.SecurityGeneration == tenant.SecurityGeneration)));
    }

    internal IQueryable<E.InstitutionTeacherProfile> TeacherProfiles(TenantContext t) =>
        db.InstitutionTeacherProfiles.AsNoTracking().Where(p => p.InstitutionId == t.InstitutionId &&
            p.MembershipId == t.MembershipId && p.InstitutionMembership.UserId == t.UserId &&
            EF.Property<Guid>(p.Teacher, "InstitutionId") == t.InstitutionId);

    internal IQueryable<E.InstitutionStudentProfile> StudentProfiles(TenantContext t) =>
        db.InstitutionStudentProfiles.AsNoTracking().Where(p => p.InstitutionId == t.InstitutionId &&
            p.MembershipId == t.MembershipId && p.InstitutionMembership.UserId == t.UserId &&
            EF.Property<Guid>(p.Student, "InstitutionId") == t.InstitutionId);

    internal IQueryable<E.ClassSubject> Assignments(TenantContext t) => Owned<E.ClassSubject>(t).Where(cs =>
        EF.Property<Guid>(cs.Class, "InstitutionId") == t.InstitutionId &&
        EF.Property<Guid>(cs.Teacher, "InstitutionId") == t.InstitutionId &&
        EF.Property<Guid>(cs.Subject, "InstitutionId") == t.InstitutionId);

    internal IQueryable<E.ClassSubject> VisibleAssignments(TenantContext t)
    {
        var rows = Assignments(t);
        if (t.Roles.Contains(InstitutionRole.InstitutionAdmin)) return rows;
        if (t.Roles.Contains(InstitutionRole.Teacher))
        { var profiles = TeacherProfiles(t); return rows.Where(cs => profiles.Any(p => p.TeacherId == cs.TeacherId)); }
        if (t.Roles.Contains(InstitutionRole.Student))
        { var profiles = StudentProfiles(t); return rows.Where(cs => profiles.Any(p => p.Student.ClassId == cs.ClassId)); }
        throw new UnauthorizedAccessException("Operazione non autorizzata.");
    }

    internal IQueryable<E.Student> VisibleStudents(TenantContext t)
    {
        var rows = Owned<E.Student>(t);
        if (t.Roles.Contains(InstitutionRole.InstitutionAdmin) || t.Roles.Contains(InstitutionRole.Staff)) return rows;
        if (!t.Roles.Contains(InstitutionRole.Teacher)) throw new UnauthorizedAccessException("Anagrafica non autorizzata.");
        var assignments = VisibleAssignments(t);
        return rows.Where(s => s.ClassId != null && EF.Property<Guid>(s.Class!, "InstitutionId") == t.InstitutionId &&
            assignments.Any(cs => cs.ClassId == s.ClassId));
    }

    internal IQueryable<E.Subject> VisibleSubjects(TenantContext t)
    {
        var rows = Owned<E.Subject>(t);
        var assignments = VisibleAssignments(t);
        return t.Roles.Contains(InstitutionRole.InstitutionAdmin) ? rows :
            rows.Where(s => assignments.Any(cs => cs.SubjectId == s.Id));
    }

    internal static void Admin(TenantContext t)
    {
        if (!t.Roles.Contains(InstitutionRole.InstitutionAdmin)) throw new UnauthorizedAccessException("Operazione amministrativa non autorizzata.");
    }

    internal async Task MembershipAsync(TenantContext t, Guid? id, string role, CancellationToken ct)
    {
        if (id is null) return;
        if (!await db.InstitutionMemberships.AsNoTracking().AnyAsync(m => m.Id == id && m.InstitutionId == t.InstitutionId &&
            m.Status == "active" && m.Institution.Status == "active" && m.InstitutionMembershipRoles.Any(r => r.Role == role), ct))
            throw new KeyNotFoundException("Membership istituzionale non disponibile.");
    }

    internal void AddOwned<T>(TenantContext t, T row) where T : class
    {
        db.Set<T>().Add(row);
        db.Entry(row).Property<Guid>("InstitutionId").CurrentValue = t.InstitutionId;
    }

    internal static async Task WriteAsync(Func<Task> action)
    {
        try { await action(); }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 or 1451 })
        { throw new BusinessRuleException("Risorsa gia presente o utilizzata nell'istituto."); }
        catch (MySqlException e) when (e.Number is 1062 or 1451)
        { throw new BusinessRuleException("Risorsa gia presente o utilizzata nell'istituto."); }
    }

    internal static KeyNotFoundException Missing() => new("Risorsa istituzionale non disponibile.");
}
