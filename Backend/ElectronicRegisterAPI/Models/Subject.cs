using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Models;

public partial class Subject
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public Guid TeacherId { get; set; }

    public virtual Teacher Teacher { get; set; } = null!;
}
