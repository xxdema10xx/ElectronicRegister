using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class Biennium
{
    public Guid Id { get; set; }

    public DateOnly StartYear { get; set; }

    public DateOnly EndYear { get; set; }

    public virtual ICollection<BienniumStudyArea> BienniumStudyAreas { get; set; } = new List<BienniumStudyArea>();
}
