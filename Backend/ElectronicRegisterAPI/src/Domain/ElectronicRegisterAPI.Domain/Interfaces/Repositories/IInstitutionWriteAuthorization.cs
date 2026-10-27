using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionWriteAuthorization
{
    /// <summary>Must be the first statements in the resource write transaction.</summary>
    Task LockAsync(TenantContext actor, CancellationToken cancellationToken = default);
    void RequireLocked(TenantContext actor);
}
