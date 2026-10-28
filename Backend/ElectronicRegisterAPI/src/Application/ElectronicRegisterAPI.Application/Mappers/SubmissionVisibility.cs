using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Application.Mappers;

internal static class SubmissionVisibility
{
    public static bool HasOfficialGrade(SubmissionStatus status, decimal? grade) => (status is SubmissionStatus.Graded or SubmissionStatus.Finalized) && grade.HasValue;
    public static SubmissionResultDto Apply(SubmissionResultDto dto, ClaimsContext? caller)
    {
        var available = HasOfficialGrade(dto.Status, dto.Grade);
        if (dto.Grade.HasValue)
            dto.Passed = dto.Grade >= 6m;
        if (caller?.Role == UserRole.Student && !available)
        {
            dto.Grade = null;
            dto.TotalScore = null;
            dto.MaxScore = null;
            dto.Passed = null;
        }

        return dto;
    }

    public static SubmissionReviewDto Apply(SubmissionReviewDto dto, ClaimsContext? caller)
    {
        var available = HasOfficialGrade(dto.Status, dto.FinalGrade);
        if (dto.FinalGrade.HasValue)
            dto.Passed = dto.FinalGrade >= 6m;
        if (caller?.Role != UserRole.Student)
            return dto;
        dto.SuggestedGrade = null;
        dto.ReviewedByUserId = null;
        foreach (var answer in dto.Answers)
        {
            answer.IsCorrect = null;
            answer.TeacherComment = null;
            // Per-answer marks can reveal the answer key, including for closed questions.
            answer.AwardedPoints = null;
            foreach (var option in answer.Options)
                option.IsCorrect = false;
        }

        if (!available)
        {
            dto.FinalGrade = null;
            dto.MaxScore = null;
            dto.PassingScore = null;
            dto.ClosedScore = null;
            dto.OpenMaxPoints = null;
            dto.OpenScore = null;
            dto.TotalScore = null;
            dto.Passed = null;
            dto.GradeId = null;
            dto.FinalizedAt = null;
        }

        return dto;
    }
}
