using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class ExamSubmission
{
    public string? DisplayOrderJson { get; set; }
    public Guid Id { get; set; }

    public Guid ExamId { get; set; }

    public Guid StudentId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime DeadlineAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public string? SubmitReason { get; set; }

    public int ViolationCount { get; set; }

    public ulong LastClientRevision { get; set; }

    public decimal? MaxScore { get; set; }

    public decimal? PassingScore { get; set; }

    public decimal? ClosedScore { get; set; }

    public decimal? OpenMaxPoints { get; set; }

    public decimal? OpenScore { get; set; }

    public decimal? TotalScore { get; set; }

    public bool? Passed { get; set; }

    public decimal? SuggestedGrade { get; set; }
    public string? OverrideReason { get; set; }

    public Guid? GradeId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Exam Exam { get; set; } = null!;

    public virtual ICollection<ExamViolation> ExamViolations { get; set; } = new List<ExamViolation>();

    public virtual Grade? Grade { get; set; }

    public virtual User? ReviewedByUser { get; set; }

    public virtual Student Student { get; set; } = null!;

    public virtual ICollection<SubmissionAnswer> SubmissionAnswers { get; set; } = new List<SubmissionAnswer>();
}
