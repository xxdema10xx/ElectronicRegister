using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Results;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface ISubmissionGradingService
{
    ClosedGradingResult GradeClosedQuestions(
        Exam exam,
        IReadOnlyList<SubmissionAnswer> answers);

    SubmissionEvaluation EvaluateAfterSubmit(
        Exam exam,
        ClosedGradingResult gradingResult);

    decimal CalculateOpenScore(
        Exam exam,
        IReadOnlyList<SubmissionAnswer> answers);

    decimal ConvertToGrade(
        decimal totalScore,
        decimal maxScore,
        decimal passingPercent);

    void EnsureReviewPointsValid(
        Question question,
        decimal awardedPoints);
}