using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Infrastructure.Mappers;

internal static class SubmissionStatusMapper
{
    public static string ToDbString(SubmissionStatus status) => status switch
    {
        SubmissionStatus.InProgress => "in_progress",
        SubmissionStatus.Graded => "graded",
        SubmissionStatus.PendingReview => "pending_review",
        SubmissionStatus.FailedAuto => "failed_auto",
        SubmissionStatus.Finalized => "finalized",
        _ => throw new ArgumentOutOfRangeException(nameof(status), $"Stato consegna non valido: {status}")};
    public static SubmissionStatus FromDbString(string status) => status.ToLowerInvariant() switch
    {
        "in_progress" => SubmissionStatus.InProgress,
        "graded" => SubmissionStatus.Graded,
        "pending_review" => SubmissionStatus.PendingReview,
        "failed_auto" => SubmissionStatus.FailedAuto,
        "finalized" => SubmissionStatus.Finalized,
        _ => throw new ArgumentOutOfRangeException(nameof(status), $"Stato consegna non valido: {status}")};
}
