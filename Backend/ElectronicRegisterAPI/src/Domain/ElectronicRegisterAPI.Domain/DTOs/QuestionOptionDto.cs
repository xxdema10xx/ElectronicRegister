namespace ElectronicRegisterAPI.Domain.DTOs;

public class QuestionOptionDto
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
    public bool IsCorrect { get; set; }
}
