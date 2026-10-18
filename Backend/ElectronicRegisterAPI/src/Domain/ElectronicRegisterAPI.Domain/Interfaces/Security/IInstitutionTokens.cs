using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IInstitutionTokens
{
    string Selection(Guid userId);
    string Access(TenantContext context);
}
public interface ICurrentTenant
{
    TenantContext Value { get; }
}
public interface ILegacyAccess
{
    Task<bool> IsAllowedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsTokenAllowedAsync(ClaimsContext caller, CancellationToken cancellationToken = default);
}
public interface IMicrosoftIdentityLink
{
    Task<Guid?> AuthenticateAsync(string token);
}
