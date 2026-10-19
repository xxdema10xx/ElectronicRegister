using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/teachers"), InstitutionSchoolEndpoint]
public sealed class InstitutionTeacherController(IInstitutionTeacherManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles = "InstitutionAdmin,Teacher,Student")]
    public async Task<ActionResult<List<TeacherDto>>> GetAll(CancellationToken ct) => Ok(await manager.GetAllAsync(ct));

    [HttpGet("count"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<ActionResult<int>> Count(CancellationToken ct) => Ok(await manager.CountAsync(ct));

    [HttpGet("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Teacher,Student")]
    public async Task<ActionResult<TeacherDto>> GetById(Guid id, CancellationToken ct) =>
        await manager.GetByIdAsync(id, ct) is { } row ? Ok(row) : NotFound();

    [HttpGet("bylastname/{lastName}"), Authorize(Roles = "InstitutionAdmin,Teacher,Student")]
    public async Task<ActionResult<List<TeacherDto>>> ByLastName(string lastName, CancellationToken ct) => Ok(await manager.GetByLastNameAsync(lastName, ct));

    [HttpGet("me"), Authorize(Roles = "Teacher")]
    public async Task<ActionResult<TeacherDto>> Me(CancellationToken ct) =>
        await manager.GetOwnProfileAsync(ct) is { } row ? Ok(row) : NotFound();

    [HttpPost, Authorize(Roles = "InstitutionAdmin")]
    public async Task<ActionResult<Guid>> Add(CreateTeacherDto dto, [FromQuery] Guid? membershipId, CancellationToken ct)
    {
        var id = await manager.AddAsync(dto, membershipId, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "InstitutionAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateTeacherDto dto, CancellationToken ct)
    { await manager.UpdateAsync(id, dto, ct); return NoContent(); }

    [HttpDelete("{id:guid}"), Authorize(Roles = "InstitutionAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    { await manager.DeleteAsync(id, ct); return NoContent(); }
}
