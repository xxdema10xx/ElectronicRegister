// Database First entity for formative_module
#nullable enable
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public partial class FormativeModule {
 public Guid Id {get;set;}
 public Guid InstitutionId {get;set;}
 public Guid VersionId {get;set;}
 public Guid UnitId {get;set;}
 public Guid? SubjectId {get;set;}
 public string? Code {get;set;}
 public string Name {get;set;} = null!;
 public string? Description {get;set;}
 public int DisplayOrder {get;set;}
 public int PlannedMinutes {get;set;}
}
