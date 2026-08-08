using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeacherController : ControllerBase
{
    private readonly ITeacherManager _teacherManager;

    public TeacherController(ITeacherManager teacherManager)
    {
        _teacherManager = teacherManager;
    }

    [HttpGet("count")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<int>> Count()
    {
        return Ok(await _teacherManager.CountAsync());
    }

    [HttpGet]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<List<TeacherDto>>> GetAll()
    {
        var teachers = await _teacherManager.GetAllAsync();
        return teachers.Count == 0 ? NotFound() : Ok(teachers);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<TeacherDto>> GetById(Guid id)
    {
        var teacher = await _teacherManager.GetByIdAsync(id);
        return teacher is null ? NotFound() : Ok(teacher);
    }

    [HttpPut("update/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Update(Guid id, UpdateTeacherDto dto)
    {
        var updated = await _teacherManager.UpdateAsync(id, dto);
        return updated ? NoContent() : NotFound();
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Add(CreateTeacherDto dto)
    {
        var id = await _teacherManager.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _teacherManager.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}