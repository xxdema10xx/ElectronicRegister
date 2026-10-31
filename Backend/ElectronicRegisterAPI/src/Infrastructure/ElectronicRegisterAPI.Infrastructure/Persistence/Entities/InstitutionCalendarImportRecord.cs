namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionCalendarImportRecord
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public long Revision { get; set; }
    public string Status { get; set; } = null!;
    public string FileSha256 { get; set; } = null!;
    public string PreviewDigest { get; set; } = null!;
    public string RawJson { get; set; } = null!;
    public string SettingsJson { get; set; } = null!;
    public string PreviewJson { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public Guid EventId { get; set; }
}
