using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionExamManager
{
    Task<List<ClassSubjectChoiceDto>> ClassSubjectsAsync();
    Task<List<StudentDto>> EligibleStudentsAsync(Guid examId);
    Task<List<ExamSummaryDto>> ExamsAsync();
    Task<ExamDetailDto?> ExamAsync(Guid id);
    Task<Guid> CreateAsync(CreateExamDto dto);
    Task UpdateAsync(Guid id, UpdateExamDto dto);
    Task AssignAsync(Guid id, UpdateExamStudentsDto dto);
    Task PublishAsync(Guid id);
    Task CloseAsync(Guid id);
    Task CancelAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<List<AssignedExamDto>> AssignedAsync();
    Task<StartExamDto> StartAsync(Guid id);
    Task<SubmissionSessionDto> SessionAsync(Guid id);
    Task<SaveAnswersResponseDto> SaveAsync(Guid id, SaveExamAnswersDto dto);
    Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto);
    Task<SubmissionResultDto> SubmitExpiredAsync(Guid id);
    Task<ViolationReceiptDto> ReportViolationAsync(Guid id, InstitutionViolationDto dto);
    Task<List<ViolationEventDto>> ViolationsAsync(Guid id);
    Task<List<SubmissionSummaryDto>> SubmissionsAsync(Guid examId, SubmissionStatus? status);
    Task<SubmissionReviewDto> ReviewAsync(Guid id);
    Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto);
    Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto);
    Task<SubmissionResultDto> ResultAsync(Guid id);
    Task<List<GradeDto>> GradesAsync();
    Task<GradeDto?> GradeAsync(Guid id);
    Task<int> GradeCountAsync();
    Task<GradePageDto> GradePageAsync(int pageNumber, int pageSize, Guid? subjectId, Guid? studentId, DateOnly? date);
    Task<GradeFiltersDto> GradeFiltersAsync();
    Task<GradeStatisticsDto?> GradeStatisticsAsync();
    Task<Guid?> AddGradeAsync(CreateGradeDto dto);
    Task<bool> UpdateGradeAsync(Guid id, UpdateGradeDto dto);
    Task<bool> DeleteGradeAsync(Guid id);
}
