using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Results;

public class SubmissionEvaluation
{
    public SubmissionStatus Status { get; set; }
    public decimal? TotalScore { get; set; }
    public bool? Passed { get; set; }
}
