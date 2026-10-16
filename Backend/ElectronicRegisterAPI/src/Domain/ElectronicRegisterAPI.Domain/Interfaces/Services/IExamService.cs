
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IExamService
{
    Task<Exam> EnsureExamExistsAsync(Guid examId);
    void EnsureValidName(string name);
    void EnsureValidSchedule(DateTime availableFrom, DateTime availableUntil, int durationMinutes);
    void EnsureValidPassingPercent(decimal passingPercent);
    void EnsureValidQuestions(IReadOnlyList<Question> questions);
    void EnsureValidRetakeExam(Exam? originalExam, Guid classSubjectId);
    void EnsureValidAssignedStudents(IReadOnlyList<ExamStudent> assignedStudents, IReadOnlyList<Student> students, Guid classId);
    void EnsureStartAllowed(Exam? exam, ExamSubmission? existingSubmission, Guid studentId, DateTime nowUtc);
    void EnsureSubmissionInProgress(ExamSubmission submission);
    void EnsureEditable(Exam exam);
    void EnsurePublishable(Exam exam, DateTime nowUtc);
    void EnsureCanCancel(Exam exam, bool hasSubmissions);
    void EnsureDeletable(Exam exam, bool hasSubmissions);
    decimal CalculateTotalPoints(IReadOnlyList<Question> questions);
}
