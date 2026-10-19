using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class InstitutionMembershipRole
{
    public Guid MembershipId { get; set; }

    public string Role { get; set; } = null!;

    public virtual InstitutionMembership Membership { get; set; } = null!;
}
