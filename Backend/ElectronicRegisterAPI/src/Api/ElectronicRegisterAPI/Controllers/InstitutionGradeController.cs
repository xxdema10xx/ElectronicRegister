using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, StaffBodyContract, InstitutionExamEndpoint, Route("api/institution/grades")]
public sealed class InstitutionGradeController(IInstitutionExamManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<List<GradeDto>>> GetAll() => Ok(await manager.GradesAsync(HttpContext.RequestAborted));
    [HttpGet("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<GradeDto>> GetById(Guid id) => await manager.GradeAsync(id, HttpContext.RequestAborted)is { } row ? Ok(row) : NotFound();
    [HttpGet("count"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<int>> Count() => Ok(await manager.GradeCountAsync(HttpContext.RequestAborted));
    [HttpGet("paged"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<GradePageDto>> Paged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] Guid? studentId = null,
        [FromQuery] DateOnly? date = null
    ) => Ok(await manager.GradePageAsync(pageNumber, pageSize, subjectId, studentId, date, HttpContext.RequestAborted));
    [HttpGet("filters"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<GradeFiltersDto>> Filters() => Ok(await manager.GradeFiltersAsync(HttpContext.RequestAborted));
    [HttpGet("statistics"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<GradeStatisticsDto>> Statistics() => await manager.GradeStatisticsAsync(HttpContext.RequestAborted)is { } row ? Ok(row) : NotFound();
    [HttpPost, Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.GradeCreate)]
    public async Task<ActionResult<Guid>> Add(CreateGradeDto dto) => await manager.AddGradeAsync(dto, HttpContext.RequestAborted)is { } id ? CreatedAtAction(nameof(GetById), new { id }, id) : NotFound();
    [HttpPut("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.GradeUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateGradeDto dto) => await manager.UpdateGradeAsync(id, dto, HttpContext.RequestAborted) ? NoContent() : NotFound();
    [HttpDelete("{id:guid}"), Authorize(Roles = "InstitutionAdmin")]
    [InstitutionPermission(InstitutionPermission.GradeDelete)]
    public async Task<IActionResult> Delete(Guid id) => await manager.DeleteGradeAsync(id, HttpContext.RequestAborted) ? NoContent() : NotFound();
}
