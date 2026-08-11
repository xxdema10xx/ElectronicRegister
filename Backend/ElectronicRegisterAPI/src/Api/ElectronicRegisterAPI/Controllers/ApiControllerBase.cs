using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ClaimsContext CurrentCaller() => new(
        UserId: Guid.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
        Role: Enum.Parse<UserRole>(User.FindFirst(ClaimTypes.Role)!.Value, ignoreCase: true),
        StudentId: Guid.TryParse(User.FindFirst(CustomClaimTypes.StudentId)?.Value, out var sId) ? sId : null,
        TeacherId: Guid.TryParse(User.FindFirst(CustomClaimTypes.TeacherId)?.Value, out var tId) ? tId : null);
}