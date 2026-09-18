using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;

public class ClassSubject
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TeacherId { get; set; }
}
