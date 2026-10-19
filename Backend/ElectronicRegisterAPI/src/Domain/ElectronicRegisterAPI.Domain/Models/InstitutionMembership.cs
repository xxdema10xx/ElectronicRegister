using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public class InstitutionMembership
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid UserId { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Pending;
    public DateTime JoinedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public List<InstitutionRole> Roles { get; set; } = new();
}
