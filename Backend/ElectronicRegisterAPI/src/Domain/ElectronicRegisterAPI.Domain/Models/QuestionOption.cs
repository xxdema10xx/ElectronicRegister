using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;

public class QuestionOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
    public bool IsCorrect { get; set; }
}
