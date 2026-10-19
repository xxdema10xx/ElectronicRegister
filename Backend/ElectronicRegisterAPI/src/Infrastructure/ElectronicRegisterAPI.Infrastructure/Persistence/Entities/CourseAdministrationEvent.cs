using System;
using System.Collections.Generic;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public partial class CourseAdministrationEvent
{
    public Guid InstitutionId { get; set; }

    public Guid Id { get; set; }

    public Guid ActorUserId { get; set; }

    public Guid ActorMembershipId { get; set; }

    public Guid CourseEditionId { get; set; }

    public Guid? EnrollmentId { get; set; }

    public Guid? CampusId { get; set; }

    public Guid? ClassId { get; set; }

    public string Action { get; set; } = null!;

    public string? PreviousStatus { get; set; }

    public string? NewStatus { get; set; }

    public DateOnly? PreviousCompletionDate { get; set; }

    public DateOnly? NewCompletionDate { get; set; }

    public long? RevisionBefore { get; set; }

    public long? RevisionAfter { get; set; }

    public string? Reason { get; set; }

    public string? RequestHash { get; set; }

    public string? BeforeJson { get; set; }

    public string AfterJson { get; set; } = null!;

    public DateTime OccurredAtUtc { get; set; }

    public virtual User ActorUser { get; set; } = null!;

    public virtual Campus? Campus { get; set; }

    public virtual Class? Class { get; set; }

    public virtual CourseEdition CourseEdition { get; set; } = null!;

    public virtual InstitutionMembership InstitutionMembership { get; set; } = null!;

    public virtual StudentEnrollment? StudentEnrollment { get; set; }
}
