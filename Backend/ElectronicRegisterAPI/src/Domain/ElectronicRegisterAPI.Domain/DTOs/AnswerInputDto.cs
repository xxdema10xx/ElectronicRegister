namespace ElectronicRegisterAPI.Domain.DTOs;

public class AnswerInputDto
{
    public Guid QuestionId { get; set; }
    public string? OpenText { get; set; }
    public List<Guid> SelectedOptionIds { get; set; } = [];
}
