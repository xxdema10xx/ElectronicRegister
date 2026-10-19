using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Exam
{
    public Guid Id { get; set; }

    public Guid ClassSubjectId { get; set; }

    public Guid? RetakeOfExamId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime AvailableFrom { get; set; }

    public DateTime AvailableUntil { get; set; }

    public int DurationMinutes { get; set; }

    public decimal PassingPercent { get; set; }

    public decimal TotalPoints { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ClassSubject ClassSubject { get; set; } = null!;

    public virtual ICollection<ExamStudent> ExamStudents { get; set; } = new List<ExamStudent>();

    public virtual ICollection<ExamSubmission> ExamSubmissions { get; set; } = new List<ExamSubmission>();

    public virtual ICollection<Exam> InverseRetakeOfExam { get; set; } = new List<Exam>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual Exam? RetakeOfExam { get; set; }
}
