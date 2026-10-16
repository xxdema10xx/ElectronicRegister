using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public class QuestionInputDto
{
    public int Position { get; set; }
    public required string Text { get; set; }
    public QuestionType Type { get; set; }
    public decimal Points { get; set; }
    public bool Required { get; set; }
    public List<QuestionOptionInputDto> Options { get; set; } = new List<QuestionOptionInputDto>();
}
