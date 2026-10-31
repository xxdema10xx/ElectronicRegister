using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionExamManager
{
    Task<List<ClassSubjectChoiceDto>> ClassSubjectsAsync(CancellationToken ct = default);
    Task<List<StudentDto>> EligibleStudentsAsync(Guid examId, CancellationToken ct = default);
    Task<List<ExamSummaryDto>> ExamsAsync(CancellationToken ct = default);
    Task<ExamDetailDto?> ExamAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateExamDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateExamDto dto, CancellationToken ct = default);
    Task AssignAsync(Guid id, UpdateExamStudentsDto dto, CancellationToken ct = default);
    Task PublishAsync(Guid id, CancellationToken ct = default);
    Task CloseAsync(Guid id, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<AssignedExamDto>> AssignedAsync(CancellationToken ct = default);
    Task<StartExamDto> StartAsync(Guid id, CancellationToken ct = default);
    Task<SubmissionSessionDto> SessionAsync(Guid id, CancellationToken ct = default);
    Task<SaveAnswersResponseDto> SaveAsync(Guid id, SaveExamAnswersDto dto, CancellationToken ct = default);
    Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, CancellationToken ct = default);
    Task<SubmissionResultDto> SubmitExpiredAsync(Guid id, CancellationToken ct = default);
    Task<ViolationReceiptDto> ReportViolationAsync(Guid id, InstitutionViolationDto dto, CancellationToken ct = default);
    Task<List<ViolationEventDto>> ViolationsAsync(Guid id, CancellationToken ct = default);
    Task<List<SubmissionSummaryDto>> SubmissionsAsync(Guid examId, SubmissionStatus? status, CancellationToken ct = default);
    Task<SubmissionReviewDto> OwnWorkAsync(Guid id, CancellationToken ct = default);
    Task<SubmissionReviewDto> ReviewAsync(Guid id, CancellationToken ct = default);
    Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto, CancellationToken ct = default);
    Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto, CancellationToken ct = default);
    Task<SubmissionResultDto> ResultAsync(Guid id, CancellationToken ct = default);
    Task<List<GradeDto>> GradesAsync(CancellationToken ct = default);
    Task<GradeDto?> GradeAsync(Guid id, CancellationToken ct = default);
    Task<int> GradeCountAsync(CancellationToken ct = default);
    Task<GradePageDto> GradePageAsync(
        int pageNumber,
        int pageSize,
        Guid? subjectId,
        Guid? studentId,
        DateOnly? date,
        CancellationToken ct = default
    );
    Task<GradeFiltersDto> GradeFiltersAsync(CancellationToken ct = default);
    Task<GradeStatisticsDto?> GradeStatisticsAsync(CancellationToken ct = default);
    Task<Guid?> AddGradeAsync(CreateGradeDto dto, CancellationToken ct = default);
    Task<bool> UpdateGradeAsync(Guid id, UpdateGradeDto dto, CancellationToken ct = default);
    Task<bool> DeleteGradeAsync(Guid id, CancellationToken ct = default);
}
