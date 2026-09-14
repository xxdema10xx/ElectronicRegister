using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Class
{
    public Guid Id { get; set; }

    public Guid BienniumStudyPathId { get; set; }

    public string Name { get; set; } = null!;

    public virtual BienniumStudyPath BienniumStudyPath { get; set; } = null!;

    public virtual ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}
