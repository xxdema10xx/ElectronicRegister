using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

// No global overload. TenantContext must be verified by the institutional manager.
public interface ICourseStructureRepository
{
    Task<EnrollmentMutationResult> MutateEnrollmentAsync(
        TenantContext tenant,
        Guid id,
        Guid eventId,
        string? targetStatus,
        DateOnly completionDate,
        string reason,
        long expectedRevision,
        bool correction,
        CancellationToken ct
    );
    Task<List<CourseAdministrationEventDto>> EnrollmentHistoryAsync(
        TenantContext tenant,
        Guid id,
        int page,
        int size,
        CancellationToken ct
    );
    Task<List<CourseAdministrationEventDto>> EditionAuditAsync(
        TenantContext tenant,
        Guid id,
        int page,
        int size,
        CancellationToken ct
    );
    Task LinkCampusAuditedAsync(
        TenantContext tenant,
        Guid editionId,
        Guid campusId,
        CancellationToken ct
    );
    Task AssignClassAuditedAsync(
        TenantContext tenant,
        Guid classId,
        Guid editionId,
        CancellationToken ct
    );
    Task<List<CourseEditionDto>> FindEditionsAsync(
        TenantContext tenant,
        Guid? studyPathId,
        int page,
        int size,
        CancellationToken ct
    );
    Task<CourseEditionDto?> FindEditionAsync(
        TenantContext tenant,
        Guid? id,
        string? code,
        CancellationToken ct
    );
    Task UpdateDescriptionAsync(
        TenantContext tenant,
        Guid id,
        UpdateCourseEditionDto dto,
        CancellationToken ct
    );
    Task<List<CampusDto>> FindCampusesAsync(
        TenantContext tenant,
        Guid? editionId,
        int page,
        int size,
        CancellationToken ct
    );
    Task<List<EditionClassDto>> FindClassesAsync(
        TenantContext tenant,
        Guid editionId,
        int page,
        int size,
        CancellationToken ct
    );
    Task<List<StudentEnrollmentDto>> FindEnrollmentsAsync(
        TenantContext tenant,
        Guid? studentId,
        Guid? editionId,
        int page,
        int size,
        CancellationToken ct
    );
    Task<StudentEnrollmentDto?> FindEnrollmentAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<bool> StudentExistsAsync(TenantContext tenant, Guid id, CancellationToken ct);
    Task<List<CourseEdition>> GetEditionsAsync(TenantContext tenant, CancellationToken ct = default);
    Task<List<StudentEnrollment>> GetEnrollmentsAsync(TenantContext tenant, Guid studentId, CancellationToken ct = default);
    Task<List<Guid>> GetCampusIdsAsync(TenantContext tenant, Guid editionId, CancellationToken ct = default);
    // HTTP writers avoid EF's generated timestamp readback by primary key alone.
    Task InsertEditionAsync(TenantContext tenant, CourseEdition edition, CancellationToken ct);
    Task InsertEnrollmentAsync(TenantContext tenant, StudentEnrollment enrollment, CancellationToken ct);
    Task AddEditionAsync(TenantContext tenant, CourseEdition edition, CancellationToken ct = default);
    Task AddEnrollmentAsync(TenantContext tenant, StudentEnrollment enrollment, CancellationToken ct = default);
    Task LinkCampusAsync(
        TenantContext tenant,
        Guid editionId,
        Guid campusId,
        CancellationToken ct = default
    );
    Task AssignClassAsync(
        TenantContext tenant,
        Guid classId,
        Guid editionId,
        CancellationToken ct = default
    );
    Task SetEnrollmentStatusAsync(
        TenantContext tenant,
        Guid id,
        string status,
        DateOnly? completionDate,
        CancellationToken ct = default
    );
}
