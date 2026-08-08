using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ClaimsContext CurrentCaller() => new(
        UserId: Guid.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
        Role: Enum.Parse<UserRole>(User.FindFirst(ClaimTypes.Role)!.Value, ignoreCase: true),
        StudentId: Guid.TryParse(User.FindFirst("studentId")?.Value, out var sId) ? sId : null,
        TeacherId: Guid.TryParse(User.FindFirst("teacherId")?.Value, out var tId) ? tId : null);
}