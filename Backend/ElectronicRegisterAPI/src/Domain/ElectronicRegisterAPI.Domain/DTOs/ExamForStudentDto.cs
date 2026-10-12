namespace ElectronicRegisterAPI.Domain.DTOs;

public class ExamForStudentDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime AvailableUntil { get; set; }
    public int DurationMinutes { get; set; }
    public decimal TotalPoints { get; set; }
    public List<QuestionForStudentDto> Questions { get; set; } = [];
}
