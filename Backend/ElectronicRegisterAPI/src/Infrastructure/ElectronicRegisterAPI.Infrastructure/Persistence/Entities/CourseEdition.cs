using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class CourseEdition
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public Guid StudyPathId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<CourseEditionCampus> CourseEditionCampuses { get; set; } = new List<CourseEditionCampus>();

    public virtual Institution Institution { get; set; } = null!;

    public virtual ICollection<StudentEnrollment> StudentEnrollments { get; set; } = new List<StudentEnrollment>();

    public virtual StudyPath StudyPath { get; set; } = null!;
}
