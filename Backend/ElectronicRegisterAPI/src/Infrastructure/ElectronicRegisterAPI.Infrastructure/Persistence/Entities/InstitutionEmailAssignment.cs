// Database First sidecar derived from migrations/institution-email/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionEmailAssignment
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? MembershipId { get; set; }
    public string Category { get; set; } = null!;
    public long PolicyRevision { get; set; }
    public string Email { get; set; } = null!;
    public string State { get; set; } = null!;
    public long Revision { get; set; }
    public string FirstNameSnapshot { get; set; } = null!;
    public string LastNameSnapshot { get; set; } = null!;
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
