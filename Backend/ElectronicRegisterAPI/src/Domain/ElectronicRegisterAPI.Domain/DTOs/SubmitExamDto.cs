namespace ElectronicRegisterAPI.Domain.DTOs;

public class SubmitExamDto
{
    public long ClientRevision { get; set; }
    public List<AnswerInputDto> Answers { get; set; } = [];
}
