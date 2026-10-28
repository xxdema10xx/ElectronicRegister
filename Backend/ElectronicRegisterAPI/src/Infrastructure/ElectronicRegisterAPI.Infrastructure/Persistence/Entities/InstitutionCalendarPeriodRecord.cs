namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionCalendarPeriodRecord
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ClassId { get; set; }
    public DateOnly MonthDate { get; set; }
    public string Status { get; set; } = null!;
    public long Revision { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public Guid? PublishedByMembershipId { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid EventId { get; set; }
}
