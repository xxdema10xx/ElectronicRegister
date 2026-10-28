using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed class SubmissionSessionDto
{
    public Guid SubmissionId { get; set; }
    public Guid ExamId { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public long CurrentRevision { get; set; }
    public required ExamForStudentDto Exam { get; set; }
    public List<AnswerInputDto> Answers { get; set; } = [];
}
