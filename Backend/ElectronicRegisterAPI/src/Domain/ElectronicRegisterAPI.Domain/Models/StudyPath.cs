using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;
public class StudyPath
{
    public Guid Id;
    public required string Name;
    public required string Description;
}
