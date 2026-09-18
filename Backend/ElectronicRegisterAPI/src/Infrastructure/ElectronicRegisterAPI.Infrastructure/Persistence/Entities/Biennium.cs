using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Biennium
{
    public Guid Id { get; set; }

    public int StartYear { get; set; }

    public int EndYear { get; set; }

    public virtual ICollection<BienniumStudyArea> BienniumStudyAreas { get; set; } = new List<BienniumStudyArea>();
}
