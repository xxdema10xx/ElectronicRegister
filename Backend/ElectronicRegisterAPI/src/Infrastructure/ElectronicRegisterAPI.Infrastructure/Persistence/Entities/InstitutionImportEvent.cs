// Database First sidecar derived from migrations/institution-import/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

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
