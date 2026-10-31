using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;

public class ExamStudent
{
    public Guid ExamId { get; set; }
    public Guid StudentId { get; set; }
    public int ExtraTimeMinutes { get; set; }
    public DateTime AssignedAt { get; set; }
}
