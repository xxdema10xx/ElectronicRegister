using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionCourseManager
{
    Task<EnrollmentMutationResult> TransitionAsync(Guid id, TransitionEnrollmentDto dto, CancellationToken ct);
    Task<EnrollmentMutationResult> CorrectCompletionAsync(Guid id, CorrectEnrollmentCompletionDto dto, CancellationToken ct);
    Task<List<CourseAdministrationEventDto>> HistoryAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct
    );
    Task<List<CourseAdministrationEventDto>> EditionAuditAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct
    );
    Task<List<CourseEditionDto>> EditionsAsync(
        Guid? studyPathId,
        int page,
        int size,
        CancellationToken ct
    );
    Task<CourseEditionDto> EditionAsync(Guid? id, string? code, CancellationToken ct);
    Task<Guid> CreateEditionAsync(CreateCourseEditionDto dto, CancellationToken ct);
    Task UpdateEditionAsync(Guid id, UpdateCourseEditionDto dto, CancellationToken ct);
    Task<List<CampusDto>> CampusesAsync(
        Guid? editionId,
        int page,
        int size,
        CancellationToken ct
    );
    Task LinkCampusAsync(Guid editionId, Guid campusId, CancellationToken ct);
    Task<List<EditionClassDto>> ClassesAsync(
        Guid editionId,
        int page,
        int size,
        CancellationToken ct
    );
    Task AssignClassAsync(Guid editionId, Guid classId, CancellationToken ct);
    Task<StudentEnrollmentDto> EnrollmentAsync(Guid id, CancellationToken ct);
    Task<List<StudentEnrollmentDto>> EnrollmentsAsync(
        Guid? studentId,
        Guid? editionId,
        int page,
        int size,
        CancellationToken ct
    );
    Task<Guid> CreateEnrollmentAsync(CreateStudentEnrollmentDto dto, CancellationToken ct);
    Task<List<OwnEnrollmentDto>> OwnEnrollmentsAsync(int page, int size, CancellationToken ct);
}
