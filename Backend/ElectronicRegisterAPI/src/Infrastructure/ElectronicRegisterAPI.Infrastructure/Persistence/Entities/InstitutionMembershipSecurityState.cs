using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class InstitutionMembershipSecurityState
{
    public Guid InstitutionId { get; set; }

    public Guid MembershipId { get; set; }

    public long SecurityGeneration { get; set; }
}
