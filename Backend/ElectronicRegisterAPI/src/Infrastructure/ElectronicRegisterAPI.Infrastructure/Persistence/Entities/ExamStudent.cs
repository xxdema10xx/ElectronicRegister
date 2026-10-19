using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class ExamStudent
{
    public Guid ExamId { get; set; }

    public Guid StudentId { get; set; }

    public int ExtraTimeMinutes { get; set; }

    public DateTime AssignedAt { get; set; }

    public virtual Exam Exam { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
