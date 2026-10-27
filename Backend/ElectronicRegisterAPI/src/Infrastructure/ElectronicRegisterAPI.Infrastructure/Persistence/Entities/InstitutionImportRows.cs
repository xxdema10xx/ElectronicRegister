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
public sealed class InstitutionImportExternalReference
{
 public Guid InstitutionId { get; set; }
 public string Category { get; set; } = null!;
 public string Reference { get; set; } = null!;
 public Guid? StudentId { get; set; }
 public Guid? TeacherId { get; set; }
}
public sealed class InstitutionImportItem
{
 public Guid InstitutionId { get; set; }
 public Guid BatchId { get; set; }
 public int RowNumber { get; set; }
 public string Category { get; set; } = null!;
 public Guid? StudentId { get; set; }
 public Guid? TeacherId { get; set; }
 public Guid? EmailAssignmentId { get; set; }
 public string? ExternalReference { get; set; }
}
public sealed class InstitutionImportEvent
{
 public Guid InstitutionId { get; set; }
 public Guid Id { get; set; }
 public Guid BatchId { get; set; }
 public long Revision { get; set; }
 public string Action { get; set; } = null!;
 public string ActorKind { get; set; } = null!;
 public Guid? ActorUserId { get; set; }
 public Guid? ActorMembershipId { get; set; }
 public string RequestHash { get; set; } = null!;
 public string ResultJson { get; set; } = null!;
 public string Reason { get; set; } = null!;
 public DateTime OccurredAtUtc { get; set; }
}
