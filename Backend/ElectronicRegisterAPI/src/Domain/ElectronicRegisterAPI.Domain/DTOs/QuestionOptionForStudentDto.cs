namespace ElectronicRegisterAPI.Domain.DTOs;

public class QuestionOptionForStudentDto
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
}
