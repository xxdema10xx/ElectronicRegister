using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Student
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public Guid? ClassId { get; set; }

    public virtual Class? Class { get; set; }

    public virtual ICollection<ExamStudent> ExamStudents { get; set; } = new List<ExamStudent>();

    public virtual ICollection<ExamSubmission> ExamSubmissions { get; set; } = new List<ExamSubmission>();

    public virtual ICollection<Grade> Grades { get; set; } = new List<Grade>();

    public virtual User? User { get; set; }
}
