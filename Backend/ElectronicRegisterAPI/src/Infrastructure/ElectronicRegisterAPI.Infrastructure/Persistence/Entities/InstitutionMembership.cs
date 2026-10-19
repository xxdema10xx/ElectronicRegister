using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class InstitutionMembership
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public Guid UserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime JoinedAt { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public virtual Institution Institution { get; set; } = null!;

    public virtual ICollection<InstitutionMembershipRole> InstitutionMembershipRoles { get; set; } = new List<InstitutionMembershipRole>();

    public virtual User User { get; set; } = null!;
}
