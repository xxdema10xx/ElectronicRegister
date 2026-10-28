using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IInstitutionTokens
{
    string Selection(Guid userId);
    string Access(TenantContext context);
}
