using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class StudyPath
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<BienniumStudyPath> BienniumStudyPaths { get; set; } = new List<BienniumStudyPath>();
}
