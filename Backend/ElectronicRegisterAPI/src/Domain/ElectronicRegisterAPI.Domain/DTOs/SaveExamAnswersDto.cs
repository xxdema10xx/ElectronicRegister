namespace ElectronicRegisterAPI.Domain.DTOs;

public class SaveExamAnswersDto
{
    public long ClientRevision { get; set; }
    public List<AnswerInputDto> Answers { get; set; } = [];
}
