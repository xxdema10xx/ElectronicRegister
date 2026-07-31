using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Models;

public partial class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public Guid? StudentId { get; set; }

    public Guid? TeacherId { get; set; }

    public virtual Student? Student { get; set; }

    public virtual Teacher? Teacher { get; set; }
}
