using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ClaimsContext CurrentCaller()
    {
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (User.Identity?.IsAuthenticated != true || !Guid.TryParse(userIdClaim, out var userId) || !Enum.TryParse<UserRole>(User.FindFirst(ClaimTypes.Role)?.Value, true, out var role) || !Enum.IsDefined(role))
            throw new UnauthorizedAccessException("Identit? non valida.");
        Guid? studentId = Guid.TryParse(User.FindFirst(CustomClaimTypes.StudentId)?.Value, out var parsedStudentId) ? parsedStudentId : null;
        Guid? teacherId = Guid.TryParse(User.FindFirst(CustomClaimTypes.TeacherId)?.Value, out var parsedTeacherId) ? parsedTeacherId : null;
        if ((role == UserRole.Student && !studentId.HasValue) || (role == UserRole.Teacher && !teacherId.HasValue))
            throw new UnauthorizedAccessException("Identità non valida.");
        return new ClaimsContext(userId, role, studentId, teacherId);
    }
}
