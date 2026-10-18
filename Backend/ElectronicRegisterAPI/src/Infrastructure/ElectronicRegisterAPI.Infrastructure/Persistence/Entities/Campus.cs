using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Campus
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Province { get; set; }

    public bool? IsActive { get; set; }

    public virtual Institution Institution { get; set; } = null!;
}
