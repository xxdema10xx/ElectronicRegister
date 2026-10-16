namespace ElectronicRegisterAPI.Domain.DTOs;

public class ReviewOpenAnswersDto
{
    public List<OpenAnswerReviewInputDto> Answers { get; set; } = new List<OpenAnswerReviewInputDto>();
}
