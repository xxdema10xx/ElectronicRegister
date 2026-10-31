using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IInstitutionImportRetention
{
    Task<int> PurgeExpiredAsync(CancellationToken ct);
}
