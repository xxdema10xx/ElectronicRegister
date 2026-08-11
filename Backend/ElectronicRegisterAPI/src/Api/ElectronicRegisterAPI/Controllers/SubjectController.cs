using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubjectController : ApiControllerBase
{
    private readonly ISubjectManager _subjectManager;

    public SubjectController(ISubjectManager subjectManager)
    {
        _subjectManager = subjectManager;
    }

    [HttpGet("count")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<int>> Count()
    {
        return Ok(await _subjectManager.CountAsync(CurrentCaller()));
    }

    [HttpGet]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<List<SubjectDto>>> GetAll()
    {
        var subjects = await _subjectManager.GetAllAsync(CurrentCaller());
        return subjects.Count == 0 ? NotFound() : Ok(subjects);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<SubjectDto>> GetById(Guid id)
    {
        var subject = await _subjectManager.GetByIdAsync(id, CurrentCaller());
        return subject is null ? NotFound() : Ok(subject);
    }

    [HttpGet("byname/{name}")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<SubjectDto>> GetSubjectByName(string name)
    {
        var subject = await _subjectManager.GetSubjectByNameAsync(name, CurrentCaller());
        return subject is null ? NotFound() : Ok(subject);
    }

    [HttpGet("byteacher/{id}")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<List<SubjectDto>>> GetSubjectByTeacherId(Guid id)
    {
        var subjects = await _subjectManager.GetSubjectsByTeacherIdAsync(id, CurrentCaller());
        return subjects.Count == 0 ? NotFound() : Ok(subjects);
    }

    [HttpPut("update/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Update(Guid id, UpdateSubjectDto dto)
    {
        var updated = await _subjectManager.UpdateAsync(id, dto);
        return updated ? NoContent() : NotFound();
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Add(CreateSubjectDto dto)
    {
        var id = await _subjectManager.AddAsync(dto);
        return id is null ? NotFound() : CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _subjectManager.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}