// Database First sidecar derived from migrations/institution-import/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionImportItem
{
    public Guid InstitutionId { get; set; }
    public Guid BatchId { get; set; }
    public int RowNumber { get; set; }
    public string Category { get; set; } = null!;
    public Guid? StudentId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? EmailAssignmentId { get; set; }
    public string? ExternalReference { get; set; }
}
