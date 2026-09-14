using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class BienniumStudyPath
{
    public Guid Id { get; set; }

    public Guid BienniumStudyAreaId { get; set; }

    public Guid StudyPathId { get; set; }

    public virtual BienniumStudyArea BienniumStudyArea { get; set; } = null!;

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual StudyPath StudyPath { get; set; } = null!;
}
