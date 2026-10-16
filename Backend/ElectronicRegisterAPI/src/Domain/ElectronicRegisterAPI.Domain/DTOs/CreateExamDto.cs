namespace ElectronicRegisterAPI.Domain.DTOs;

public class CreateExamDto
{
    public Guid ClassSubjectId { get; set; }
    public Guid? RetakeOfExamId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime AvailableFrom { get; set; }
    public DateTime AvailableUntil { get; set; }
    public int DurationMinutes { get; set; }
    public decimal PassingPercent { get; set; } = 60m;
    public List<ExamStudentInputDto> AssignedStudents { get; set; } = new List<ExamStudentInputDto>();
    public List<QuestionInputDto> Questions { get; set; } = new List<QuestionInputDto>();
}
