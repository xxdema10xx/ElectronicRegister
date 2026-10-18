using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class InstitutionStudentProfile
{
    public Guid InstitutionId { get; set; }

    public Guid MembershipId { get; set; }

    public Guid StudentId { get; set; }

    public virtual InstitutionMembership InstitutionMembership { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
