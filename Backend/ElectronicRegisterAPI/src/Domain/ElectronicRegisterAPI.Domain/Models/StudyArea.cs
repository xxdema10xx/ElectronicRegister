using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;
public class StudyArea
{
    public Guid Id;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}
