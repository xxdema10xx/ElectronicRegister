using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models
{
    public class Exam
    {
        public Guid Id { get; set; }
        public Guid ClassSubjectId { get; set; }
        public Guid? RetakeOfExamId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public DateTime AvailableFrom { get; set; }
        public DateTime AvailableUntil { get; set; }
        public int DurationMinutes { get; set; }
        public decimal PassingPercent { get; set; }
        public decimal TotalPoints { get; set; }
        public ExamStatus Status { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public List<Question> Questions { get; set; } = new List<Question>();
        public List<ExamStudent> AssignedStudents { get; set; } = new List<ExamStudent>();
        public bool IsRetake => RetakeOfExamId.HasValue;
    }
}
