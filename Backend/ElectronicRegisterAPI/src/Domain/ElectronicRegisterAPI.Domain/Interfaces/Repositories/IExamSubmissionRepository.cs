using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Results;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IExamSubmissionRepository
{
    Task<ExamSubmission?> GetByIdAsync(Guid id, bool withAnswers = false);
    Task<ExamSubmission?> GetByExamAndStudentAsync(Guid examId, Guid studentId, bool withAnswers = false);
    Task<List<ExamSubmission>> GetByExamAsync(Guid examId, SubmissionStatus? status = null);
    Task<List<ExamSubmission>> GetByStudentAsync(Guid studentId);
    Task<List<ExamSubmission>> GetExpiredInProgressAsync(DateTime utcNow, int take);
    Task AddAsync(ExamSubmission submission);
    Task<SaveAnswersResult> SaveAnswersAsync(Guid submissionId, long clientRevision, IReadOnlyList<SubmissionAnswer> answers);
    Task<bool> TryBeginSubmitAsync(Guid submissionId, DateTime submittedAt, SubmitReason reason);
    Task UpdateAfterGradingAsync(ExamSubmission submission);
    Task UpdateAfterReviewAsync(ExamSubmission submission);
    Task<ExamSubmission?> GetByIdForUpdateAsync(Guid id, bool withAnswers = false);
}
