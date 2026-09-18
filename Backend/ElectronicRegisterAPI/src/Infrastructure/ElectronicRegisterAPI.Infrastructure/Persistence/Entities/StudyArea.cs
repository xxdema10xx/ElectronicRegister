using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class StudyArea
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<BienniumStudyArea> BienniumStudyAreas { get; set; } = new List<BienniumStudyArea>();
}
