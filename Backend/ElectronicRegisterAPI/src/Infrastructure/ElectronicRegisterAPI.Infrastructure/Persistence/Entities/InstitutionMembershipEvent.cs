namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

// Additive database-first sidecar for migrations/mt5/iam-d/001_membership_event.sql.
public sealed class InstitutionMembershipEvent
{
    public Guid InstitutionId { get; set; }
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public Guid TargetMembershipId { get; set; }
    public string Action { get; set; } = null!;
    public string? TargetRole { get; set; }
    public string PreviousStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
    public string PreviousRoles { get; set; } = null!;
    public string NewRoles { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public long RevisionBefore { get; set; }
    public long RevisionAfter { get; set; }
    public string RequestHash { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
}
