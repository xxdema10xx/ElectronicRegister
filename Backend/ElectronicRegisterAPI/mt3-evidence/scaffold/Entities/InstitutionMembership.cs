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

    public virtual InstitutionStudentProfile? InstitutionStudentProfile { get; set; }

    public virtual InstitutionTeacherProfile? InstitutionTeacherProfile { get; set; }
}
