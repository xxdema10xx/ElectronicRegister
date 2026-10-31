using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface ICurrentTenant
{
    TenantContext Value { get; }
}
