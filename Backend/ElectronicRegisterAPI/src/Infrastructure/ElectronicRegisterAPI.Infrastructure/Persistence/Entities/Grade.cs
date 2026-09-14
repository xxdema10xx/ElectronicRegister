using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Grade
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid ClassSubjectId { get; set; }

    public decimal Value { get; set; }

    public DateOnly Date { get; set; }

    public virtual ClassSubject ClassSubject { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
