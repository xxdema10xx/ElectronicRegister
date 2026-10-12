namespace ElectronicRegisterAPI.Domain.DTOs;

public class OpenAnswerReviewInputDto
{
    public Guid AnswerId { get; set; }
    public decimal AwardedPoints { get; set; }
    public string? Comment { get; set; }
}
