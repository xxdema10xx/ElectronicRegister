// Database First entity for formative_plan_version
#nullable enable
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public partial class FormativePlanVersion {
 public Guid Id {get;set;}
 public Guid InstitutionId {get;set;}
 public Guid StudyPathId {get;set;}
 public Guid PlanId {get;set;}
 public int VersionNumber {get;set;}
 public string Name {get;set;} = null!;
 public string Status {get;set;} = null!;
 public long Revision {get;set;}
 public int TheoryPlannedMinutes {get;set;}
 public int InternshipPlannedMinutes {get;set;}
 public DateTime CreatedAtUtc {get;set;}
 public DateTime? PublishedAtUtc {get;set;}
}
