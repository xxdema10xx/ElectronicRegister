using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Business.Services;

public class SubmissionGradingService : ISubmissionGradingService
{
    public ClosedGradingResult GradeClosedQuestions(Exam exam, IReadOnlyList<SubmissionAnswer> answers)
    {
        if (exam is null) throw new ArgumentNullException(nameof(exam));
        if (answers is null) throw new ArgumentNullException(nameof(answers));

        decimal closedScore = 0m;
        decimal openMaxPoints = 0m;

        var questionById = exam.Questions.ToDictionary(q => q.Id);

        foreach (var q in exam.Questions)
        {
            if (q.Type == QuestionType.Open)
            {
                openMaxPoints += q.Points;
                continue;
            }

            var ans = answers.FirstOrDefault(a => a.QuestionId == q.Id);

            if (ans is null)
            {
                // missing => zero
                continue;
            }

            var selected = ans.SelectedOptionIds?.ToHashSet() ?? new HashSet<Guid>();

            var correctOptionIds = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();

            if (q.Type == QuestionType.Single)
            {
                // single: exactly one correct and selected equals that
                if (selected.SetEquals(correctOptionIds) && correctOptionIds.Count == 1)
                {
                    closedScore += q.Points;
                }
            }
            else if (q.Type == QuestionType.Multiple)
            {
                // multiple: exact match required
                if (selected.SetEquals(correctOptionIds))
                {
                    closedScore += q.Points;
                }
            }
        }

        var maxClosed = exam.Questions.Where(q => q.Type != QuestionType.Open).Sum(q => q.Points);
        var maxScore = maxClosed + openMaxPoints;
        var passingScore = maxScore * exam.PassingPercent / 100m;

        return new ClosedGradingResult
        {
            ClosedScore = closedScore,
            OpenMaxPoints = openMaxPoints,
            MaxScore = maxScore,
            PassingScore = passingScore
        };
    }

    public SubmissionEvaluation EvaluateAfterSubmit(Exam exam, ClosedGradingResult gradingResult)
    {
        if (exam is null) throw new ArgumentNullException(nameof(exam));
        if (gradingResult is null) throw new ArgumentNullException(nameof(gradingResult));

        // If there are open questions, require manual review unless openMaxPoints == 0
        var hasOpen = gradingResult.OpenMaxPoints > 0m;

        var status = hasOpen ? SubmissionStatus.PendingReview : SubmissionStatus.Graded;

        var total = gradingResult.ClosedScore + 0m; // open score to be added later

        bool? passed = null;
        if (!hasOpen)
        {
            passed = gradingResult.ClosedScore + 0m >= gradingResult.PassingScore;
        }

        return new SubmissionEvaluation
        {
            Status = status,
            TotalScore = hasOpen ? (decimal?)null : gradingResult.ClosedScore,
            Passed = passed
        };
    }

    public decimal CalculateOpenScore(Exam exam, IReadOnlyList<SubmissionAnswer> answers)
    {
        if (exam is null) throw new ArgumentNullException(nameof(exam));
        if (answers is null) throw new ArgumentNullException(nameof(answers));

        // Only consider answers that belong to open questions
        var openQuestionIds = exam.Questions.Where(q => q.Type == QuestionType.Open).Select(q => q.Id).ToHashSet();

        return answers.Where(a => openQuestionIds.Contains(a.QuestionId)).Sum(a => a.AwardedPoints ?? 0m);
    }

    public decimal ConvertToGrade(decimal totalScore, decimal maxScore, decimal passingPercent)
    {
        // Validate inputs according to Phase 2B rules
        if (maxScore <= 0) throw new ArgumentOutOfRangeException(nameof(maxScore), "maxScore must be > 0");
        if (totalScore < 0) throw new ArgumentOutOfRangeException(nameof(totalScore), "totalScore must be >= 0");
        if (totalScore > maxScore) throw new ArgumentOutOfRangeException(nameof(totalScore), "totalScore cannot exceed maxScore");
        if (passingPercent <= 0 || passingPercent >= 100) throw new ArgumentOutOfRangeException(nameof(passingPercent), "passingPercent must be between 0 and 100 (exclusive)");

        // decimal arithmetic only
        // p = TotalScore / MaxScore
        var p = maxScore == 0m ? 0m : Decimal.Divide(totalScore, maxScore);
        // s = PassingPercent / 100
        var s = Decimal.Divide(passingPercent, 100m);

        decimal grade;

        if (p <= s)
        {
            // grade = 1 + 5 * (p / s)
            // avoid division by zero (s validated to be >0)
            grade = 1m + 5m * Decimal.Divide(p, s);
            // requirement: when totalScore < passingScore, rounded grade must never be >= 6
            // we therefore cap raw grade to a value strictly below 6 when p < s
            if (p < s && grade >= 6m)
            {
                grade = 5.99m;
            }
        }
        else
        {
            // grade = 6 + 4 * ((p - s) / (1 - s))
            grade = 6m + 4m * Decimal.Divide((p - s), (1m - s));
        }

        // clamp to [1,10]
        if (grade < 1m) grade = 1m;
        if (grade > 10m) grade = 10m;

        // round to 2 decimals using MidpointRounding.AwayFromZero
        var rounded = Decimal.Round(grade, 2, MidpointRounding.AwayFromZero);

        // enforce the rule: if totalScore < passingScore, rounded must be < 6
        if (totalScore < Decimal.Multiply(maxScore, s) && rounded >= 6m)
        {
            // set to 5.99 explicitly
            return 5.99m;
        }

        return rounded;
    }

    public void EnsureReviewPointsValid(Question question, decimal awardedPoints)
    {
        if (question is null) throw new ArgumentNullException(nameof(question));
        if (awardedPoints < 0 || awardedPoints > question.Points)
            throw new ArgumentException("Punteggio non valido per la domanda.");
    }
}
