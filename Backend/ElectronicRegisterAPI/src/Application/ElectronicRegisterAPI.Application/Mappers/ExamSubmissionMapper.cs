using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Mappers;

internal static class ExamSubmissionMapper
{
    public static SaveAnswersResponseDto ToSaveResponseDto(SaveAnswersResult r)
    {
        if (r is null) throw new ArgumentNullException(nameof(r));

        return new SaveAnswersResponseDto
        {
            Accepted = r.Accepted,
            CurrentRevision = r.CurrentRevision,
            SavedAt = r.SavedAt
        };
    }

    public static SubmissionResultDto ToSubmissionResultDto(ExamSubmission s)
    {
        if (s is null) throw new ArgumentNullException(nameof(s));

        return new SubmissionResultDto
        {
            SubmissionId = s.Id,
            ExamId = s.ExamId,
            Status = s.Status,
            SubmittedAt = s.SubmittedAt,
            TotalScore = s.TotalScore,
            MaxScore = s.MaxScore,
            Grade = s.GradeId is null ? (decimal?)null : s.ClosedScore, // best-effort
            Passed = s.Passed
        };
    }
}
