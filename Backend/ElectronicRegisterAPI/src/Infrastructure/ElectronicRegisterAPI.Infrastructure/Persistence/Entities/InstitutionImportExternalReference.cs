// Database First sidecar derived from migrations/institution-import/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionImportExternalReference
{
    public Guid InstitutionId { get; set; }
    public string Category { get; set; } = null!;
    public string Reference { get; set; } = null!;
    public Guid? StudentId { get; set; }
    public Guid? TeacherId { get; set; }
}
