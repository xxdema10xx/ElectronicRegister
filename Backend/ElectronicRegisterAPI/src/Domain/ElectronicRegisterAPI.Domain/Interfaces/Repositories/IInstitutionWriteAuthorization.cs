using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionWriteAuthorization
{
    /// <summary>Must be the first statements in the resource write transaction.</summary>
    Task LockAsync(TenantContext actor, CancellationToken cancellationToken = default);
    /// <summary>Exam creation also takes the tenant mutex; acquire it exclusively up front, never upgrade a shared lock.</summary>
    Task LockExamAsync(TenantContext actor, CancellationToken cancellationToken = default);
    void RequireLocked(TenantContext actor);
}
