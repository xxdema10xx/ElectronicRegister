using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IMicrosoftIdentityLink
{
    Task<Guid?> AuthenticateAsync(string token);
}
