using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Business.Services;

internal class SubmissionGradingService : ISubmissionGradingService
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

    public decimal CalculateOpenScore(IReadOnlyList<SubmissionAnswer> answers)
    {
        if (answers is null) throw new ArgumentNullException(nameof(answers));

        // Open score is determined by awarded points already set on answers
        return answers.Sum(a => a.AwardedPoints ?? 0m);
    }

    public decimal ConvertToGrade(decimal totalScore, decimal maxScore, decimal passingPercent)
    {
        if (maxScore <= 0) return 0m;

        // convert to 1..10 scale, passingPercent is threshold for pass (~6)
        var ratio = totalScore / maxScore;
        var grade = 1m + ratio * 9m; // linear mapping

        // clamp
        if (grade < 1m) grade = 1m;
        if (grade > 10m) grade = 10m;

        return Math.Round(grade, 2);
    }

    public void EnsureReviewPointsValid(Question question, decimal awardedPoints)
    {
        if (question is null) throw new ArgumentNullException(nameof(question));
        if (awardedPoints < 0 || awardedPoints > question.Points)
            throw new ArgumentException("Punteggio non valido per la domanda.");
    }
}
