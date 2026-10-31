using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, StaffBodyContract, InstitutionExamEndpoint, Route("api/institution/exams")]
public sealed class InstitutionExamController(IInstitutionExamManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<List<ExamSummaryDto>>> GetAll() => Ok(await manager.ExamsAsync(HttpContext.RequestAborted));
    [HttpGet("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<ExamDetailDto>> GetById(Guid id) => await manager.ExamAsync(id, HttpContext.RequestAborted)is { } row ? Ok(row) : NotFound();
    [HttpGet("assigned"), Authorize(Roles = "Student")]
    public async Task<ActionResult<List<AssignedExamDto>>> Assigned() => Ok(await manager.AssignedAsync(HttpContext.RequestAborted));
    [HttpGet("{id:guid}/eligible-students"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<List<StudentDto>>> EligibleStudents(Guid id) => Ok(await manager.EligibleStudentsAsync(id, HttpContext.RequestAborted));
    [HttpPost, Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.ExamCreate)]
    public async Task<ActionResult<Guid>> Add(CreateExamDto dto)
    {
        var id = await manager.CreateAsync(dto, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.ExamUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateExamDto dto)
    {
        await manager.UpdateAsync(id, dto, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPut("{id:guid}/students"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.ExamAssign)]
    public async Task<IActionResult> Assign(Guid id, UpdateExamStudentsDto dto)
    {
        await manager.AssignAsync(id, dto, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.ExamPublish)]
    public async Task<IActionResult> Publish(Guid id)
    {
        await manager.PublishAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("{id:guid}/close"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    [InstitutionPermission(InstitutionPermission.ExamClose)]
    public async Task<IActionResult> Close(Guid id)
    {
        await manager.CloseAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    [InstitutionPermission(InstitutionPermission.ExamCancel)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await manager.CancelAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    [InstitutionPermission(InstitutionPermission.ExamDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await manager.DeleteAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("{id:guid}/start"), Authorize(Roles = "Student")]
    public async Task<ActionResult<StartExamDto>> Start(Guid id) => Ok(await manager.StartAsync(id, HttpContext.RequestAborted));
    [HttpGet("{id:guid}/submissions"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<List<SubmissionSummaryDto>>> Submissions(Guid id, [FromQuery] SubmissionStatus? status) => Ok(await manager.SubmissionsAsync(id, status, HttpContext.RequestAborted));
}
