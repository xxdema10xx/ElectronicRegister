using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class ExamViolation
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    public string Type { get; set; } = null!;

    public string? MetadataJson { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual ExamSubmission Submission { get; set; } = null!;
}
