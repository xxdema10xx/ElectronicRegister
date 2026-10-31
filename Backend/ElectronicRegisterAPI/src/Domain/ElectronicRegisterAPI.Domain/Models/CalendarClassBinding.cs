namespace ElectronicRegisterAPI.Domain.Models;

// Values must be loaded tenant-scoped from authoritative rows, not deserialized from a client.
public sealed record CalendarClassBinding(Guid InstitutionId, Guid ClassId, Guid? CourseEditionId);
