using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Question
{
    public Guid Id { get; set; }

    public Guid ExamId { get; set; }

    public int Position { get; set; }

    public string Text { get; set; } = null!;

    public string Type { get; set; } = null!;

    public decimal Points { get; set; }

    public bool Required { get; set; }

    public virtual Exam Exam { get; set; } = null!;

    public virtual ICollection<QuestionOption> QuestionOptions { get; set; } = new List<QuestionOption>();

    public virtual ICollection<SubmissionAnswer> SubmissionAnswers { get; set; } = new List<SubmissionAnswer>();
}
