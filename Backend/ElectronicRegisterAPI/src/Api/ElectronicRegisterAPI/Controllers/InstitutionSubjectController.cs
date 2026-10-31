using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/subjects"), InstitutionSchoolEndpoint]
public sealed class InstitutionSubjectController(IInstitutionSubjectManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles = "InstitutionAdmin,Teacher,Student")]
    public async Task<ActionResult<List<SubjectDto>>> GetAll(CancellationToken ct) => Ok(await manager.GetAllAsync(ct));
    [HttpGet("count"), Authorize(Roles = "InstitutionAdmin,Teacher,Student")]
    public async Task<ActionResult<int>> Count(CancellationToken ct) => Ok(await manager.CountAsync(ct));
    [HttpGet("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Teacher,Student")]
    public async Task<ActionResult<SubjectDto>> GetById(Guid id, CancellationToken ct) => await manager.GetByIdAsync(id, ct)is { } row ? Ok(row) : NotFound();
    [HttpGet("byname/{name}"), Authorize(Roles = "InstitutionAdmin,Teacher,Student")]
    public async Task<ActionResult<SubjectDto>> ByName(string name, CancellationToken ct) => await manager.GetByNameAsync(name, ct)is { } row ? Ok(row) : NotFound();
    [HttpGet("byteacher/{teacherId:guid}"), Authorize(Roles = "InstitutionAdmin,Teacher,Student")]
    public async Task<ActionResult<List<SubjectDto>>> ByTeacher(Guid teacherId, CancellationToken ct) => Ok(await manager.GetByTeacherAsync(teacherId, ct));
    [HttpPost, Authorize(Roles = "InstitutionAdmin")]
    public async Task<ActionResult<Guid>> Add(CreateSubjectDto dto, CancellationToken ct)
    {
        var id = await manager.AddAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "InstitutionAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateSubjectDto dto, CancellationToken ct)
    {
        await manager.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = "InstitutionAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await manager.DeleteAsync(id, ct);
        return NoContent();
    }
}
