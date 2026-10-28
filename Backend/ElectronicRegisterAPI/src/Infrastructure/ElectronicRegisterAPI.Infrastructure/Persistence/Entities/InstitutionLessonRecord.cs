#nullable enable
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public sealed class InstitutionLessonRecord {
 public Guid Id {get;set;}
 public Guid InstitutionId {get;set;}
 public Guid PeriodId {get;set;}
 public Guid ClassId {get;set;}
 public Guid CourseEditionId {get;set;}
 public Guid VersionId {get;set;}
 public Guid ModuleId {get;set;}
 public Guid SubjectId {get;set;}
 public Guid ClassSubjectId {get;set;}
 public Guid OriginalTeacherId {get;set;}
 public Guid TeacherId {get;set;}
 public DateOnly LocalDate {get;set;}
 public TimeOnly StartsAt {get;set;}
 public TimeOnly EndsAt {get;set;}
 public string TimeZoneId {get;set;} = null!;
 public DateTime StartUtc {get;set;}
 public DateTime EndUtc {get;set;}
 public string Publication {get;set;} = null!;
 public string Status {get;set;} = null!;
 public long Revision {get;set;}
 public int? DeliveredMinutes {get;set;}
 public Guid? ConfirmedByUserId {get;set;}
 public Guid? ConfirmedByMembershipId {get;set;}
 public DateTime? ConfirmedAtUtc {get;set;}
 public Guid? RecoveryOfId {get;set;}
 public Guid? ImportId {get;set;}
 public string? SubstitutionReason {get;set;}
 public string? Room {get;set;}
 public string? Description {get;set;}
 public string? ExternalReference {get;set;}
 public Guid EventId {get;set;}
}
