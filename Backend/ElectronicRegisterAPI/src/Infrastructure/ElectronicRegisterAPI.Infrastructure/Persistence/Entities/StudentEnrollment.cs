using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class StudentEnrollment
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseEditionId { get; set; }

    public DateOnly EnrollmentDate { get; set; }

    public DateOnly? CompletionDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual CourseEdition CourseEdition { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
