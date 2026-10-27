// Database First sidecar derived from migrations/institution-email/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public sealed class InstitutionEmailMutex
{
 public int Id { get; set; }
}
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
public sealed class InstitutionEmailReservation
{
 public string Address { get; set; } = null!;
 public Guid? UserId { get; set; }
 public Guid? InstitutionId { get; set; }
 public Guid? AssignmentId { get; set; }
}
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
