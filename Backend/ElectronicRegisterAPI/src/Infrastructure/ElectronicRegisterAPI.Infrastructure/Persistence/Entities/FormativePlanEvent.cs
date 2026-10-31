// Database First entity for formative_plan_event
#nullable enable
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public partial class FormativePlanEvent {
 public Guid Id {get;set;}
 public Guid InstitutionId {get;set;}
 public Guid PlanId {get;set;}
 public Guid? VersionId {get;set;}
 public Guid? CourseEditionId {get;set;}
 public Guid ActorUserId {get;set;}
 public Guid ActorMembershipId {get;set;}
 public string Operation {get;set;} = null!;
 public long? RevisionBefore {get;set;}
 public long RevisionAfter {get;set;}
 public string RequestHash {get;set;} = null!;
 public string? BeforeJson {get;set;}
 public string AfterJson {get;set;} = null!;
 public string Reason {get;set;} = null!;
 public DateTime OccurredAtUtc {get;set;}
}
