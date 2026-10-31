namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionCalendarOccupancyRecord
{
    public Guid InstitutionId { get; set; }
    public Guid LessonId { get; set; }
    public string ResourceKind { get; set; } = null!;
    public Guid ResourceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public Guid EventId { get; set; }
}
