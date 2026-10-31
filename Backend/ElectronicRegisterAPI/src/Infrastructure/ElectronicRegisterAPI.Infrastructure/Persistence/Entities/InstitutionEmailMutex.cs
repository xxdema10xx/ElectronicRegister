// Database First sidecar derived from migrations/institution-email/001_expand.sql.
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

public sealed class InstitutionEmailMutex
{
    public int Id { get; set; }
}
