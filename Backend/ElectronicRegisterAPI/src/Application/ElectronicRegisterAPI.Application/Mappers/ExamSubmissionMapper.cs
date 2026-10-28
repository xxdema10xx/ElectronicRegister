using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Mappers;

internal static class ExamSubmissionMapper
{
    public static SaveAnswersResponseDto ToSaveResponseDto(SaveAnswersResult r)
    {
        if (r is null)
            throw new ArgumentNullException(nameof(r));
        return new SaveAnswersResponseDto
        {
            Accepted = r.Accepted,
            CurrentRevision = r.CurrentRevision,
            SavedAt = r.SavedAt
        };
    }

    public static SubmissionResultDto ToSubmissionResultDto(ExamSubmission s, decimal? officialGradeValue, ClaimsContext? caller)
    {
        if (s is null)
            throw new ArgumentNullException(nameof(s));
        return SubmissionVisibility.Apply(new SubmissionResultDto { SubmissionId = s.Id, ExamId = s.ExamId, Status = s.Status, SubmittedAt = s.SubmittedAt, SubmitReason = s.SubmitReason, TotalScore = s.TotalScore, MaxScore = s.MaxScore, // officialGradeValue is provided by the application layer when GradeId is present
 Grade = s.GradeId is null ? (decimal? )null : officialGradeValue, Passed = s.Passed }, caller);
    }
}
