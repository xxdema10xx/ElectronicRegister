using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models
{
    public partial class ExamViolation
    {
        public Guid Id { get; set; }

        public Guid SubmissionId { get; set; }

        public required string Type { get; set; }

        public string? MetadataJson { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}
