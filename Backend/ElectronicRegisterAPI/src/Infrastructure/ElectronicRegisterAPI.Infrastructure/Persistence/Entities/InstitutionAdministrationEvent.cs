namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionAdministrationEvent
{
    public Guid InstitutionId { get; set; }
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public long Revision { get; set; }
    public string Action { get; set; } = null!;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
