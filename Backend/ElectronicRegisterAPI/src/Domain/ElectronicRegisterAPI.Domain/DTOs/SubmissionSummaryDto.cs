using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public class SubmissionSummaryDto
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid StudentId { get; set; }
    public required string StudentFirstName { get; set; }
    public required string StudentLastName { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public SubmitReason? SubmitReason { get; set; }
    public int ViolationCount { get; set; }
    public decimal? TotalScore { get; set; }
    public decimal? MaxScore { get; set; }
    public bool? Passed { get; set; }
    public Guid? GradeId { get; set; }
}
