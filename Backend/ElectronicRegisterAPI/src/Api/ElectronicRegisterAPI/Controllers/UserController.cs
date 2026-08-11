using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly IUserManager _userManager;

    public UsersController(IUserManager userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("count")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<int>> Count()
    {
        return Ok(await _userManager.CountAsync());
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        var users = await _userManager.GetAllAsync();
        return users.Count == 0 ? NotFound() : Ok(users);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
    {
        var user = await _userManager.GetByIdAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("update/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Update(Guid id, UpdateUserDto dto)
    {
        var updated = await _userManager.UpdateAsync(id, dto);
        return updated ? NoContent() : NotFound();
    }

    [HttpPut("updatepassword/{id}")]
    [Authorize(Roles = "student,teacher,admin")]
    public async Task<ActionResult> UpdatePassword(Guid id, UpdatePasswordDto dto)
    {
        var updated = await _userManager.UpdatePasswordAsync(id, dto, CurrentCaller());
        return updated ? NoContent() : NotFound();
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Add(CreateUserDto dto)
    {
        var id = await _userManager.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _userManager.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}