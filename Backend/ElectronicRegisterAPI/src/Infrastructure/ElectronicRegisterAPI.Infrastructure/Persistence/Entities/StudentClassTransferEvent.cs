namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class StudentClassTransferEvent
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid FromClassId { get; set; }
    public Guid ToClassId { get; set; }
    public Guid FromCourseEditionId { get; set; }
    public Guid ToCourseEditionId { get; set; }
    public Guid SourceEnrollmentId { get; set; }
    public Guid DestinationEnrollmentId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorMembershipId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly SourceCourseStartDate { get; set; }
    public DateOnly DestinationCourseStartDate { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public int AppliedLimitMonths { get; set; }
    public long RevisionBefore { get; set; }
    public long RevisionAfter { get; set; }
    public string Reason { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public string FromClassName { get; set; } = null!;
    public string ToClassName { get; set; } = null!;
    public string FromCourseName { get; set; } = null!;
    public string ToCourseName { get; set; } = null!;
}
