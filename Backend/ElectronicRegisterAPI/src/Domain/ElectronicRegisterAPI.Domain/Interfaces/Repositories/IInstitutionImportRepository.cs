using ElectronicRegisterAPI.Domain.Interfaces.Persistence;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionImportRepository
{
    Task<T> ExecuteAsync<T>(TenantContext actor, Func<IInstitutionImportSession, Task<T>> action, CancellationToken ct);
}
