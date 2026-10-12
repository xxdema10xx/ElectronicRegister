using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models
{
    public class Question
    {
        public Guid Id { get; set; }
        public Guid ExamId { get; set; }

        public int Position { get; set; }

        public required string Text { get; set; }

        public QuestionType Type { get; set; }

        public decimal Points { get; set; }

        public bool Required { get; set; }

        public List<QuestionOption> Options { get; set; } = [];
    }
}
