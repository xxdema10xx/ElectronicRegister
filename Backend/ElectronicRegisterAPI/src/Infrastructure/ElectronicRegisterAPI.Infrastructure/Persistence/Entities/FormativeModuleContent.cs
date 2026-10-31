// Database First entity for formative_module_content
#nullable enable
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
public partial class FormativeModuleContent {
 public Guid Id {get;set;}
 public Guid InstitutionId {get;set;}
 public Guid VersionId {get;set;}
 public Guid ModuleId {get;set;}
 public string Description {get;set;} = null!;
 public int DisplayOrder {get;set;}
}
