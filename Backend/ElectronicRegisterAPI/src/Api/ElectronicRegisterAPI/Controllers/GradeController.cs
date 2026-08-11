using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GradeController : ApiControllerBase
{
    private readonly IGradeManager _gradeManager;

    public GradeController(IGradeManager gradeManager)
    {
        _gradeManager = gradeManager;
    }

    [HttpGet("count")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<int>> Count()
    {
        var count = await _gradeManager.CountAsync(CurrentCaller());
        return Ok(count);
    }

    [HttpGet("statistics")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<GradeStatisticsDto>> GetStatistics()
    {
        var statistics = await _gradeManager.GetStatisticsAsync(CurrentCaller());
        return statistics is null ? NotFound() : Ok(statistics); ;
    }

    [HttpGet("filters")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<GradeFiltersDto>> GetFiltered()
    {
        var filters = await _gradeManager.GetFiltersAsync(CurrentCaller());
        return Ok(filters);
    }

    [HttpGet("paged")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<GradePageDto>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] DateOnly? date = null
    )
    {
        var grades = await _gradeManager.GetPagedAsync(
            pageNumber,
            pageSize,
            subjectId,
            studentId,
            date,
            CurrentCaller()
        );
        return Ok(grades);
    }

    [HttpGet]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<List<GradeDto>>> GetAll()
    {
        var grades = await _gradeManager.GetAllAsync(CurrentCaller());
        return Ok(grades);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<GradeDto>> GetById(Guid id)
    {
        var grade = await _gradeManager.GetByIdAsync(id, CurrentCaller());
        return grade is null ? NotFound() : Ok(grade);
    }

    [HttpGet("bystudentid/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<List<GradeDto>>> GetGradesByStudentId(Guid id)
    {
        var grades = await _gradeManager.GetGradesByStudentIdAsync(id);
        return grades.Count == 0 ? NotFound() : Ok(grades);
    }

    [HttpGet("bysubject/{subject}")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<List<GradeDto>>> GetGradesBySubjectName(string subject)
    {
        var grades = await _gradeManager.GetGradesBySubjectNameAsync(subject, CurrentCaller());
        return grades is null || grades.Count == 0 ? NotFound() : Ok(grades);
    }

    [HttpGet("bydate/{date}")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<List<GradeDto>>> GetGradesByDate(DateOnly date)
    {
        var grades = await _gradeManager.GetGradesByDateAsync(date, CurrentCaller());
        return grades is null || grades.Count == 0 ? NotFound() : Ok(grades);
    }

    [HttpPost]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Add(CreateGradeDto dto)
    {
        var id = await _gradeManager.AddAsync(dto, CurrentCaller());
        return id is null ? NotFound() : CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("update/{id}")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Update(Guid id, UpdateGradeDto dto)
    {
        var updated = await _gradeManager.UpdateAsync(id, dto, CurrentCaller());
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _gradeManager.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}