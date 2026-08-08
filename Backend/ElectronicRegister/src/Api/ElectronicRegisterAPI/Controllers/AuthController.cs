using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthManager _authManager;

    public AuthController(IAuthManager authManager)
    {
        _authManager = authManager;
    }

    [HttpPost("login")]
    public async Task<ActionResult> Login(LoginDto dto)
    {
        var token = await _authManager.LoginAsync(dto);
        return token is null ? Unauthorized("Credenziali non valide") : Ok(new { token });
    }

    [HttpPost("microsoft-login")]
    public async Task<ActionResult> MicrosoftLogin([FromBody] MicrosoftLoginDto dto)
    {
        if (dto is null || string.IsNullOrEmpty(dto.AccessToken))
            return BadRequest("Access token is required.");

        var token = await _authManager.MicrosoftLoginAsync(dto);
        return token is null
            ? Unauthorized("Utente non registrato nel sistema o token Microsoft non valido.")
            : Ok(new { token });
    }

    [HttpPost("register")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Register(RegisterDto dto)
    {
        await _authManager.RegisterAsync(dto);
        return Ok("Registration successful!");
    }

    [HttpPost("RegisterForAdmin")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> RegisterForAdmin(RegisterForAdminDto dto)
    {
        await _authManager.RegisterForAdminAsync(dto);
        return Ok("Registration successful!");
    }

    [HttpGet("me")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var user = await _authManager.GetCurrentUserAsync(CurrentCaller());
        return user is null ? NotFound() : Ok(user);
    }
}