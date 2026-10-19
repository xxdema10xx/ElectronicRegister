namespace ElectronicRegisterAPI.Domain.Results;

public class SaveAnswersResult
{
    public bool Accepted { get; set; }

    public long CurrentRevision { get; set; }

    public DateTime? SavedAt { get; set; }
}