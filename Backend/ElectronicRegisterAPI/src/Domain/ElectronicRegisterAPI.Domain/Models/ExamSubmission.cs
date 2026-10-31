using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public class ExamSubmission
{
    public string? DisplayOrderJson { get; set; }
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid StudentId { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public long Revision { get; set; }
    public decimal? MaxScore { get; set; }
    public decimal? PassingScore { get; set; }
    public decimal? ClosedScore { get; set; }
    public decimal? OpenScore { get; set; }
    public decimal? TotalScore { get; set; }
    public bool? Passed { get; set; }
    public Guid? GradeId { get; set; }
    // additional fields mapped from persistence
    public SubmitReason? SubmitReason { get; set; }
    public int ViolationCount { get; set; }
    public decimal? OpenMaxPoints { get; set; }
    public decimal? SuggestedGrade { get; set; }
    public string? OverrideReason { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<SubmissionAnswer> Answers { get; set; } = new List<SubmissionAnswer>();
}
