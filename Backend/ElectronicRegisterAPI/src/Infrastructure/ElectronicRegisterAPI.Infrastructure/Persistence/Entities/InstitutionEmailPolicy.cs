// Database First sidecar derived from migrations/institution-email/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionEmailPolicy
{
    public Guid InstitutionId { get; set; }
    public Guid PolicyId { get; set; }
    public string Category { get; set; } = null!;
    public long Revision { get; set; }
    public string State { get; set; } = null!;
    public string Configuration { get; set; } = null!;
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
