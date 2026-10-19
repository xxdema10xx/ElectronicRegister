using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class InstitutionTeacherProfile
{
    public Guid InstitutionId { get; set; }

    public Guid MembershipId { get; set; }

    public Guid TeacherId { get; set; }

    public virtual InstitutionMembership InstitutionMembership { get; set; } = null!;

    public virtual Teacher Teacher { get; set; } = null!;
}
