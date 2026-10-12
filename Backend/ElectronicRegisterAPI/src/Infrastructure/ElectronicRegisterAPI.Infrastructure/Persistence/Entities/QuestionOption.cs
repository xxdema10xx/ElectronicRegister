using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class QuestionOption
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }

    public int Position { get; set; }

    public string Text { get; set; } = null!;

    public bool IsCorrect { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual ICollection<SubmissionAnswer> SubmissionAnswers { get; set; } = new List<SubmissionAnswer>();
}
