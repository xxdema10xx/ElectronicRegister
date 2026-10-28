using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface ILegacyAccess
{
    Task<bool> IsAllowedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsTokenAllowedAsync(ClaimsContext caller, CancellationToken cancellationToken = default);
}
