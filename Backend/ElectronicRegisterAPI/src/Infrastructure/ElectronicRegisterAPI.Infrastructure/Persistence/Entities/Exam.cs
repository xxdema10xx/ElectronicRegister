using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Exam
{
    public Guid Id { get; set; }
    public Guid ClassSubjectId { get; set; }
    public DateOnly Date { get; set; }
    public virtual ClassSubject ClassSubject { get; set; } = null!;
}
