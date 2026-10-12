using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public class AssignedExamDto
{
    public Guid Id { get; set; }
    public Guid ClassSubjectId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime AvailableFrom { get; set; }
    public DateTime AvailableUntil { get; set; }
    public int DurationMinutes { get; set; }
    public int ExtraTimeMinutes { get; set; }
    public ExamStatus Status { get; set; }
    public SubmissionStatus? SubmissionStatus { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? DeadlineAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool CanStart { get; set; }
    public bool IsRetake { get; set; }
}
