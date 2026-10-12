namespace ElectronicRegisterAPI.Domain.DTOs;

public class StartExamDto
{
    public Guid SubmissionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public required ExamForStudentDto Exam { get; set; }
}
