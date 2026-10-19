using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class SubmissionAnswer
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    public Guid QuestionId { get; set; }

    public string? OpenText { get; set; }

    public bool? IsCorrect { get; set; }

    public decimal? AwardedPoints { get; set; }

    public string? TeacherComment { get; set; }

    public DateTime LastSavedAt { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual ExamSubmission Submission { get; set; } = null!;

    public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}
