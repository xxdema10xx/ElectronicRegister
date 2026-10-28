using ElectronicRegisterAPI.Domain.Policies;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class CourseStructureRepository
{
    private IQueryable<E.CourseEdition> Editions(TenantContext t) => db.CourseEditions.AsNoTracking().Where(e => e.InstitutionId == t.InstitutionId);
    private static IQueryable<CourseEditionDto> EditionDtos(IQueryable<E.CourseEdition> query) => query.Select(e => new CourseEditionDto(e.Id, e.InstitutionId, e.StudyPathId, e.Code, e.Name, e.StartDate, e.EndDate, e.Status, e.CreatedAt, e.UpdatedAt, ""));
    private static CourseEditionDto Versioned(CourseEditionDto e) => e with
    {
        Version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e with { Version = "" }))))
    };
    public async Task<List<CourseEditionDto>> FindEditionsAsync(
        TenantContext t,
        Guid? path,
        int page,
        int size,
        CancellationToken ct
    )
    {
        var q = Editions(t);
        if (path is { } p)
            q = q.Where(e => e.StudyPathId == p);
        var rows = q.OrderBy(e => e.Code).ThenBy(e => e.Id).Skip((page - 1) * size).Take(size);
        return (await EditionDtos(rows).ToListAsync(ct)).Select(Versioned).ToList();
    }

    public async Task<CourseEditionDto?> FindEditionAsync(
        TenantContext t,
        Guid? id,
        string? code,
        CancellationToken ct
    )
    {
        if (id is null && code is null)
            throw new ArgumentException("Identificatore richiesto.");
        var q = Editions(t);
        q = id is { } key ? q.Where(e => e.Id == key) : q.Where(e => e.Code == code);
        var e = await EditionDtos(q).SingleOrDefaultAsync(ct);
        return e is null ? null : Versioned(e);
    }

    public async Task UpdateDescriptionAsync(
        TenantContext t,
        Guid id,
        UpdateCourseEditionDto input,
        CancellationToken ct
    )
    {
        // A scoped row lock plus the opaque snapshot token prevents lost updates, including identical timestamps.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM course_edition WHERE institution_id={t.InstitutionId} AND id={id} FOR UPDATE", ct);
        var e = await FindEditionAsync(t, id, null, ct) ?? throw Missing();
        if (!string.Equals(e.Version, input.Version, StringComparison.Ordinal))
            throw new CourseConflictException();
        CourseStructureValidation.Edition(new(id, t.InstitutionId, e.StudyPathId, input.Code, input.Name, input.StartDate, input.EndDate, e.Status));
        // Existing DATETIME timestamps use the database session clock; do not mix them with an application UTC clock.
        var now = await db.Database.SqlQueryRaw<DateTime>("SELECT CURRENT_TIMESTAMP(6) AS Value").SingleAsync(ct);
        if (now <= e.UpdatedAt)
            now = e.UpdatedAt.AddTicks(10);
        await db.CourseEditions.Where(x => x.InstitutionId == t.InstitutionId && x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Code, input.Code).SetProperty(x => x.Name, input.Name).SetProperty(x => x.StartDate, input.StartDate).SetProperty(x => x.EndDate, input.EndDate).SetProperty(x => x.UpdatedAt, now), ct);
        await AppendEventAsync(t, Guid.NewGuid(), id, null, null, null, "course-edition.update", null, null, null, null, null, null, null, null, JsonSerializer.Serialize(new { e.Code, e.Name, e.StartDate, e.EndDate }), JsonSerializer.Serialize(new { input.Code, input.Name, input.StartDate, input.EndDate }), ct);
        await tx.CommitAsync(ct);
    }

    public Task<List<CampusDto>> FindCampusesAsync(
        TenantContext t,
        Guid? editionId,
        int page,
        int size,
        CancellationToken ct
    )
    {
        var q = db.Campuses.AsNoTracking().Where(e => e.InstitutionId == t.InstitutionId);
        if (editionId is { } id)
            q = q.Where(e => db.CourseEditionCampuses.Any(l => l.InstitutionId == t.InstitutionId && l.CourseEditionId == id && l.CampusId == e.Id));
        return q.OrderBy(e => e.Name).ThenBy(e => e.Id).Skip((page - 1) * size).Take(size).Select(e => new CampusDto(e.Id, e.Name, e.Address, e.City, e.Province, e.IsActive)).ToListAsync(ct);
    }

    public Task<List<EditionClassDto>> FindClassesAsync(
        TenantContext t,
        Guid editionId,
        int page,
        int size,
        CancellationToken ct
    ) => db.Classes.AsNoTracking().Where(e => EF.Property<Guid>(e, "InstitutionId") == t.InstitutionId && EF.Property<Guid?>(e, "CourseEditionId") == editionId).OrderBy(e => e.Name).ThenBy(e => e.Id).Skip((page - 1) * size).Take(size).Select(e => new EditionClassDto(e.Id, e.Name, e.BienniumStudyPathId, editionId)).ToListAsync(ct);
    private IQueryable<E.StudentEnrollment> Enrollments(TenantContext t) => db.StudentEnrollments.AsNoTracking().Where(e => e.InstitutionId == t.InstitutionId && e.CourseEdition.InstitutionId == t.InstitutionId);
    private static IQueryable<StudentEnrollmentDto> EnrollmentDtos(IQueryable<E.StudentEnrollment> query) => query.Select(e => new StudentEnrollmentDto(e.Id, e.CourseEditionId, e.CourseEdition.Code, e.CourseEdition.Name, e.StudentId, e.EnrollmentDate, e.CompletionDate, e.Status, EF.Property<long>(e, "Revision")));
    public Task<List<StudentEnrollmentDto>> FindEnrollmentsAsync(
        TenantContext t,
        Guid? studentId,
        Guid? editionId,
        int page,
        int size,
        CancellationToken ct
    )
    {
        var q = Enrollments(t);
        if (studentId is { } s)
            q = q.Where(e => e.StudentId == s);
        if (editionId is { } e)
            q = q.Where(x => x.CourseEditionId == e);
        return EnrollmentDtos(q.OrderBy(e => e.EnrollmentDate).ThenBy(e => e.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
    }

    public Task<StudentEnrollmentDto?> FindEnrollmentAsync(TenantContext t, Guid id, CancellationToken ct) => EnrollmentDtos(Enrollments(t).Where(e => e.Id == id)).SingleOrDefaultAsync(ct);
    public Task<bool> StudentExistsAsync(TenantContext t, Guid id, CancellationToken ct) => db.Students.AnyAsync(e => e.Id == id && EF.Property<Guid>(e, "InstitutionId") == t.InstitutionId, ct);
}
