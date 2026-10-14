using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;
public class ItsClass
{
    public Guid Id { get; set; }
    public Guid BienniumStudyPathId { get; set; }
    public required string Name { get; set; }
}
