using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public class SubmissionAnswerReviewDto
{
    public Guid AnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public int Position { get; set; }
    public required string QuestionText { get; set; }
    public QuestionType QuestionType { get; set; }
    public decimal MaxPoints { get; set; }
    public bool Required { get; set; }
    public string? OpenText { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? AwardedPoints { get; set; }
    public string? TeacherComment { get; set; }
    public List<SubmissionAnswerOptionReviewDto> Options { get; set; } = new List<SubmissionAnswerOptionReviewDto>();
}
