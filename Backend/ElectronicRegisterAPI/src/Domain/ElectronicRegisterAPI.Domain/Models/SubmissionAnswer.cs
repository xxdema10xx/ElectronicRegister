using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models
{
    public class SubmissionAnswer
    {
        public Guid Id { get; set; }
        public Guid SubmissionId { get; set; }
        public Guid QuestionId { get; set; }
        public string? OpenText { get; set; }
        public bool? IsCorrect { get; set; }
        public decimal? AwardedPoints { get; set; }
        public string? TeacherComment { get; set; }
        public List<Guid> SelectedOptionIds { get; set; } = new List<Guid>();
    }
}
