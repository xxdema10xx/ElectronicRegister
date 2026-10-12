using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public class SubmissionResultDto
{
    public Guid SubmissionId { get; set; }
    public Guid ExamId { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public SubmitReason? SubmitReason { get; set; }
    public decimal? TotalScore { get; set; }
    public decimal? MaxScore { get; set; }
    public decimal? Grade { get; set; }
    public bool? Passed { get; set; }
}
