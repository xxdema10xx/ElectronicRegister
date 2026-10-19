namespace ElectronicRegisterAPI.Domain.DTOs;

public class ExamAssignedStudentDto
{
    public Guid StudentId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public int ExtraTimeMinutes { get; set; }
    public DateTime AssignedAt { get; set; }
}
