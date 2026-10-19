namespace ElectronicRegisterAPI.Domain.DTOs;

public class QuestionOptionInputDto
{
    public int Position { get; set; }
    public required string Text { get; set; }
    public bool IsCorrect { get; set; }
}
