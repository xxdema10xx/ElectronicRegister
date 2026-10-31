// Database First sidecar derived from migrations/institution-email/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionEmailReservation
{
    public string Address { get; set; } = null!;
    public Guid? UserId { get; set; }
    public Guid? InstitutionId { get; set; }
    public Guid? AssignmentId { get; set; }
}
