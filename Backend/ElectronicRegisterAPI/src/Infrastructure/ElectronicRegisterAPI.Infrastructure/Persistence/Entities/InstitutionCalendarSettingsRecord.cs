namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionCalendarSettingsRecord
{
    public Guid InstitutionId { get; set; }
    public string TimeZoneId { get; set; } = null!;
    public int ApprovalLeadDays { get; set; }
    public long Revision { get; set; }
    public Guid EventId { get; set; }
}
