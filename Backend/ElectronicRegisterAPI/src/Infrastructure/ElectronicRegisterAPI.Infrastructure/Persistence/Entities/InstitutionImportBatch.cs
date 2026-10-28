// Database First sidecar derived from migrations/institution-import/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionImportBatch
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public string Category { get; set; } = null!;
    public string State { get; set; } = null!;
    public long Revision { get; set; }
    public string FileSha256 { get; set; } = null!;
    public string PreviewDigest { get; set; } = null!;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string? Payload { get; set; }
    public string? ResultJson { get; set; }
    public string WarningsJson { get; set; } = null!;
    public long? PolicyRevision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
