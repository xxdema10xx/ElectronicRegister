using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentController : ControllerBase
{
    private readonly IStudentManager _studentManager;

    public StudentController(IStudentManager studentManager)
    {
        _studentManager = studentManager;
    }

    [HttpGet("count")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<int>> Count()
    {
        return Ok(await _studentManager.CountAsync());
    }

    [HttpGet]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<List<StudentDto>>> GetAll()
    {
        var students = await _studentManager.GetAllAsync();
        return students.Count == 0 ? NotFound() : Ok(students);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<StudentDto>> GetById(Guid id)
    {
        var student = await _studentManager.GetByIdAsync(id);
        return student is null ? NotFound() : Ok(student);
    }

    [HttpGet("bylastname/{lastName}")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<List<StudentDto>>> GetStudentsByName(string lastName)
    {
        var students = await _studentManager.GetStudentsByLastNameAsync(lastName);
        return students.Count == 0 ? NotFound() : Ok(students);
    }

    [HttpPut("update/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Update(Guid id, UpdateStudentDto dto)
    {
        var updated = await _studentManager.UpdateAsync(id, dto);
        return updated ? NoContent() : NotFound();
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Add(CreateStudentDto dto)
    {
        var id = await _studentManager.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _studentManager.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}