namespace ElectronicRegisterAPI.Domain.DTOs;

public class ExamStudentInputDto
{
    public Guid StudentId { get; set; }
    public int ExtraTimeMinutes { get; set; }
}
