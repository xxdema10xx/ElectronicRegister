// Database First sidecar derived from migrations/institution-email/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionEmailEvent
{
    public Guid InstitutionId { get; set; }
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public string Operation { get; set; } = null!;
    public string Category { get; set; } = null!;
    public long PolicyRevision { get; set; }
    public Guid? AssignmentId { get; set; }
    public string RequestHash { get; set; } = null!;
    public string? BeforeJson { get; set; }
    public string AfterJson { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
}
