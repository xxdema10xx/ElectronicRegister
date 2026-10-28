using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class CourseStructureRepository(ElectronicRegisterContext db) : ICourseStructureRepository
{
    public Task<List<CourseEdition>> GetEditionsAsync(TenantContext tenant, CancellationToken ct = default) => db.CourseEditions.AsNoTracking().Where(e => e.InstitutionId == tenant.InstitutionId).OrderBy(e => e.Code).Select(e => new CourseEdition(e.Id, e.InstitutionId, e.StudyPathId, e.Code, e.Name, e.StartDate, e.EndDate, e.Status)).ToListAsync(ct);
    public Task<List<StudentEnrollment>> GetEnrollmentsAsync(TenantContext tenant, Guid studentId, CancellationToken ct = default) => db.StudentEnrollments.AsNoTracking().Where(e => e.InstitutionId == tenant.InstitutionId && e.StudentId == studentId).OrderBy(e => e.EnrollmentDate).ThenBy(e => e.Id).Select(e => new StudentEnrollment(e.Id, e.InstitutionId, e.StudentId, e.CourseEditionId, e.EnrollmentDate, e.CompletionDate, e.Status)).ToListAsync(ct);
    public Task<List<Guid>> GetCampusIdsAsync(TenantContext tenant, Guid editionId, CancellationToken ct = default) => db.CourseEditionCampuses.AsNoTracking().Where(e => e.InstitutionId == tenant.InstitutionId && e.CourseEditionId == editionId).Select(e => e.CampusId).ToListAsync(ct);
    public async Task AddEditionAsync(TenantContext tenant, CourseEdition edition, CancellationToken ct = default)
    {
        await ValidateEditionAsync(tenant, edition, ct);
        db.CourseEditions.Add(new E.CourseEdition { Id = edition.Id, InstitutionId = tenant.InstitutionId, StudyPathId = edition.StudyPathId, Code = edition.Code, Name = edition.Name, StartDate = edition.StartDate, EndDate = edition.EndDate, Status = edition.Status });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddEnrollmentAsync(TenantContext tenant, StudentEnrollment enrollment, CancellationToken ct = default)
    {
        await ValidateEnrollmentAsync(tenant, enrollment, ct);
        db.StudentEnrollments.Add(new E.StudentEnrollment { Id = enrollment.Id, InstitutionId = tenant.InstitutionId, StudentId = enrollment.StudentId, CourseEditionId = enrollment.CourseEditionId, EnrollmentDate = enrollment.EnrollmentDate, CompletionDate = enrollment.CompletionDate, Status = enrollment.Status });
        await db.SaveChangesAsync(ct);
    }

    public async Task LinkCampusAsync(
        TenantContext tenant,
        Guid editionId,
        Guid campusId,
        CancellationToken ct = default
    )
    {
        if (!await db.CourseEditions.AnyAsync(e => e.Id == editionId && e.InstitutionId == tenant.InstitutionId, ct) || !await db.Campuses.AnyAsync(e => e.Id == campusId && e.InstitutionId == tenant.InstitutionId, ct))
            throw Missing();
        db.CourseEditionCampuses.Add(new E.CourseEditionCampus { InstitutionId = tenant.InstitutionId, CourseEditionId = editionId, CampusId = campusId });
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignClassAsync(
        TenantContext tenant,
        Guid classId,
        Guid editionId,
        CancellationToken ct = default
    )
    {
        var row = await db.Classes.SingleOrDefaultAsync(e => e.Id == classId && EF.Property<Guid>(e, "InstitutionId") == tenant.InstitutionId, ct) ?? throw Missing();
        var path = await db.BienniumStudyPaths.Where(e => e.Id == row.BienniumStudyPathId && EF.Property<Guid>(e, "InstitutionId") == tenant.InstitutionId).Select(e => e.StudyPathId).SingleAsync(ct);
        if (!await db.CourseEditions.AnyAsync(e => e.Id == editionId && e.InstitutionId == tenant.InstitutionId && e.StudyPathId == path, ct))
            throw Missing();
        // Existing attribution requires a verified migration manifest, never an implicit move.
        var current = db.Entry(row).Property<Guid?>("CourseEditionId");
        if (current.CurrentValue is not null && current.CurrentValue != editionId)
            throw new InvalidOperationException("Classe gia attribuita.");
        current.CurrentValue = editionId;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetEnrollmentStatusAsync(
        TenantContext tenant,
        Guid id,
        string status,
        DateOnly? completionDate,
        CancellationToken ct = default
    )
    {
        var row = await db.StudentEnrollments.SingleOrDefaultAsync(e => e.Id == id && e.InstitutionId == tenant.InstitutionId, ct) ?? throw Missing();
        CourseStructureValidation.Enrollment(new(row.Id, row.InstitutionId, row.StudentId, row.CourseEditionId, row.EnrollmentDate, completionDate, status));
        row.Status = status;
        row.CompletionDate = completionDate;
        if (db.Entry(row).Property<long>("Revision").CurrentValue == long.MaxValue)
            throw new InvalidOperationException("Revision esaurita.");
        db.Entry(row).Property<long>("Revision").CurrentValue++;
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateEditionAsync(TenantContext tenant, CourseEdition edition, CancellationToken ct)
    {
        CourseStructureValidation.Edition(edition);
        Owner(tenant, edition.InstitutionId);
        if (!await db.StudyPaths.AnyAsync(e => e.Id == edition.StudyPathId && EF.Property<Guid>(e, "InstitutionId") == tenant.InstitutionId, ct))
            throw Missing();
    }

    private async Task ValidateEnrollmentAsync(TenantContext tenant, StudentEnrollment enrollment, CancellationToken ct)
    {
        CourseStructureValidation.Enrollment(enrollment);
        Owner(tenant, enrollment.InstitutionId);
        if (!await db.Students.AnyAsync(e => e.Id == enrollment.StudentId && EF.Property<Guid>(e, "InstitutionId") == tenant.InstitutionId, ct) || !await db.CourseEditions.AnyAsync(e => e.Id == enrollment.CourseEditionId && e.InstitutionId == tenant.InstitutionId, ct))
            throw Missing();
    }

    // Single parameterized INSERT, defaults remain owned by the database. No unscoped EF readback of timestamps.
    public async Task InsertEditionAsync(TenantContext tenant, CourseEdition e, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ValidateEditionAsync(tenant, e, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO `course_edition` (id,institution_id,study_path_id,code,name,start_date,end_date,status) VALUES ({e.Id},{tenant.InstitutionId},{e.StudyPathId},{e.Code},{e.Name},{e.StartDate},{e.EndDate},{e.Status})", ct);
        await AppendEventAsync(tenant, Guid.NewGuid(), e.Id, null, null, null, "course-edition.create", null, null, null, null, null, null, null, null, null, System.Text.Json.JsonSerializer.Serialize(e), ct);
        await tx.CommitAsync(ct);
    }

    public async Task InsertEnrollmentAsync(TenantContext tenant, StudentEnrollment e, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ValidateEnrollmentAsync(tenant, e, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO `student_enrollment` (id,institution_id,student_id,course_edition_id,enrollment_date,completion_date,status) VALUES ({e.Id},{tenant.InstitutionId},{e.StudentId},{e.CourseEditionId},{e.EnrollmentDate},{e.CompletionDate},{e.Status})", ct);
        await AppendEventAsync(tenant, Guid.NewGuid(), e.CourseEditionId, e.Id, null, null, "enrollment.create", null, e.Status, null, e.CompletionDate, null, 0, null, null, null, System.Text.Json.JsonSerializer.Serialize(e), ct);
        await tx.CommitAsync(ct);
    }

    private static void Owner(TenantContext tenant, Guid owner)
    {
        if (tenant.InstitutionId != owner)
            throw Missing();
    }

    private static KeyNotFoundException Missing() => new("Risorsa istituzionale non disponibile.");
}
