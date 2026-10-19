namespace ElectronicRegisterAPI.Domain.DTOs;

public class SaveAnswersResponseDto
{
    public bool Accepted { get; set; }
    public long CurrentRevision { get; set; }
    public DateTime? SavedAt { get; set; }
}
