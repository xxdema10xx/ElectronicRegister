using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class IdentityLifecycleEvent
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Transition { get; set; } = null!;

    public string RuleCode { get; set; } = null!;

    public int RuleVersion { get; set; }

    public long SecurityGeneration { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
}
