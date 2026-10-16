using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public class QuestionDto
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
    public QuestionType Type { get; set; }
    public decimal Points { get; set; }
    public bool Required { get; set; }
    public List<QuestionOptionDto> Options { get; set; } = new List<QuestionOptionDto>();
}
