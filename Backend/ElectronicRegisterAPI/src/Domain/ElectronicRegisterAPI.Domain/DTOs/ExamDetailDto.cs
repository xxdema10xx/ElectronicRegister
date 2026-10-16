using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public class ExamDetailDto
{
    public Guid Id { get; set; }
    public Guid ClassSubjectId { get; set; }
    public Guid? RetakeOfExamId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime AvailableFrom { get; set; }
    public DateTime AvailableUntil { get; set; }
    public int DurationMinutes { get; set; }
    public decimal PassingPercent { get; set; }
    public decimal TotalPoints { get; set; }
    public ExamStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<QuestionDto> Questions { get; set; } = new List<QuestionDto>();
    public List<ExamAssignedStudentDto> AssignedStudents { get; set; } = new List<ExamAssignedStudentDto>();
    public bool IsRetake => RetakeOfExamId.HasValue;
}
