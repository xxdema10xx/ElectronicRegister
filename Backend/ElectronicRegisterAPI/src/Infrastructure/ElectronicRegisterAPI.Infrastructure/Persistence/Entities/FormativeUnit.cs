// Database First entity for formative_unit
#nullable enable
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public partial class FormativeUnit {
 public Guid Id {get;set;}
 public Guid InstitutionId {get;set;}
 public Guid VersionId {get;set;}
 public string? Code {get;set;}
 public string Name {get;set;} = null!;
 public string? Description {get;set;}
 public int DisplayOrder {get;set;}
}
