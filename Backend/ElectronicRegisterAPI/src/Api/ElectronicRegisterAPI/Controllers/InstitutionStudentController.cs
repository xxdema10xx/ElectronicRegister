using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, StaffBodyContract, Route("api/institution/students"), InstitutionSchoolEndpoint]
public sealed class InstitutionStudentController(IInstitutionStudentManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<List<StudentDto>>> GetAll([FromQuery] Guid? classId, CancellationToken ct) =>
        Ok(classId.HasValue ? await manager.GetForClassAsync(classId.Value, ct) : await manager.GetAllAsync(ct));

    [HttpGet("count"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<int>> Count(CancellationToken ct) => Ok(await manager.CountAsync(ct));

    [HttpGet("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<StudentDto>> GetById(Guid id, CancellationToken ct) =>
        await manager.GetByIdAsync(id, ct) is { } row ? Ok(row) : NotFound();

    [HttpGet("bylastname/{lastName}"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<List<StudentDto>>> ByLastName(string lastName, CancellationToken ct) => Ok(await manager.GetByLastNameAsync(lastName, ct));

    [HttpGet("me"), Authorize(Roles = "Student")]
    public async Task<ActionResult<StudentDto>> Me(CancellationToken ct) =>
        await manager.GetOwnProfileAsync(ct) is { } row ? Ok(row) : NotFound();

    [HttpPost, Authorize(Roles = "InstitutionAdmin,Staff")]
    [InstitutionPermission(InstitutionPermission.UserCreate)]
    public async Task<ActionResult<Guid>> Add(CreateStudentDto dto, [FromQuery] Guid? classId, [FromQuery] Guid? membershipId, CancellationToken ct)
    {
        var id = await manager.AddAsync(dto, classId, membershipId, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Staff")]
    [InstitutionPermission(InstitutionPermission.UserUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateStudentDto dto, CancellationToken ct)
    { await manager.UpdateAsync(id, dto, ct); return NoContent(); }

    [HttpDelete("{id:guid}"), Authorize(Roles = "InstitutionAdmin")]
    [InstitutionPermission(InstitutionPermission.UserDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    { await manager.DeleteAsync(id, ct); return NoContent(); }
}
