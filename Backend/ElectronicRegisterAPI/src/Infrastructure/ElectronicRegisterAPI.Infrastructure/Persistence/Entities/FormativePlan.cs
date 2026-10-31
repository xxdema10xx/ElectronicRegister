// Database First entity for formative_plan
#nullable enable
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public partial class FormativePlan {
 public Guid Id {get;set;}
 public Guid InstitutionId {get;set;}
 public Guid StudyPathId {get;set;}
 public string Code {get;set;} = null!;
 public string Name {get;set;} = null!;
 public string? Description {get;set;}
 public long Revision {get;set;}
 public DateTime CreatedAtUtc {get;set;}
}
