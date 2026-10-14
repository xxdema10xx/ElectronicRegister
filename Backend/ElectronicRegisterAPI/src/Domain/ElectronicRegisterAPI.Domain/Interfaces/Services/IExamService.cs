
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IExamService
{
    void EnsureValidName(string name);
    void EnsureValidSchedule(DateTime availableFrom, DateTime availableUntil, int durationMinutes);
    void EnsureValidPassingPercent(decimal passingPercent);
    void EnsureValidQuestions(IReadOnlyList<Question> questions);
    void EnsureEditable(Exam exam);
    void EnsurePublishable(Exam exam, DateTime nowUtc);
    void EnsureCanCancel(Exam exam, bool hasSubmissions);
    void EnsureDeletable(Exam exam, bool hasSubmissions);
    decimal CalculateTotalPoints(IReadOnlyList<Question> questions);
}
