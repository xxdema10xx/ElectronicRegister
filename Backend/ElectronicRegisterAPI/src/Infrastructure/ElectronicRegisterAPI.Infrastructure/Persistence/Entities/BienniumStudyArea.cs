using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class BienniumStudyArea
{
    public Guid Id { get; set; }

    public Guid BienniumId { get; set; }

    public Guid StudyAreaId { get; set; }

    public virtual Biennium Biennium { get; set; } = null!;

    public virtual ICollection<BienniumStudyPath> BienniumStudyPaths { get; set; } = new List<BienniumStudyPath>();

    public virtual StudyArea StudyArea { get; set; } = null!;
}
