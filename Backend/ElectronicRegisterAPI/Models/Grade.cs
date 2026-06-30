using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Models;

public partial class Grade
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid TeacherId { get; set; }

    public decimal Value { get; set; }

    public DateOnly Date { get; set; }
}
