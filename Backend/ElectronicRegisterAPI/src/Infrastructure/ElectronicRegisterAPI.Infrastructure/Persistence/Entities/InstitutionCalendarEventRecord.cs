namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionCalendarEventRecord
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public string ResourceKind { get; set; } = null!;
    public Guid ResourceId { get; set; }
    public string Operation { get; set; } = null!;
    public long? RevisionBefore { get; set; }
    public long RevisionAfter { get; set; }
    public string RequestHash { get; set; } = null!;
    public string? BeforeJson { get; set; }
    public string StateJson { get; set; } = null!;
    public string ReceiptJson { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
}
