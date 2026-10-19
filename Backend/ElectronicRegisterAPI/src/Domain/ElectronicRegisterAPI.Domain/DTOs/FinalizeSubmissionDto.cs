namespace ElectronicRegisterAPI.Domain.DTOs;

public class FinalizeSubmissionDto
{
    // null = usa il voto suggerito calcolato dal server.
    public decimal? FinalGrade { get; set; }
    public string? OverrideReason { get; set; }
}
