using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class CourseEditionCampus
{
    public Guid InstitutionId { get; set; }

    public Guid CourseEditionId { get; set; }

    public Guid CampusId { get; set; }

    public virtual Campus Campus { get; set; } = null!;

    public virtual CourseEdition CourseEdition { get; set; } = null!;
}
