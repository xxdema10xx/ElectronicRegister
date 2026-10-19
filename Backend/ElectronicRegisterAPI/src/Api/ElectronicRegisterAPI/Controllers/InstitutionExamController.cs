using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionExamEndpoint, Route("api/institution/exams")]
public sealed class InstitutionExamController(IInstitutionExamManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<ActionResult<List<ExamSummaryDto>>> GetAll() => Ok(await manager.ExamsAsync());
    [HttpGet("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<ActionResult<ExamDetailDto>> GetById(Guid id) => await manager.ExamAsync(id) is { } row ? Ok(row) : NotFound();
    [HttpGet("assigned"), Authorize(Roles = "Student")]
    public async Task<ActionResult<List<AssignedExamDto>>> Assigned() => Ok(await manager.AssignedAsync());
    [HttpGet("{id:guid}/eligible-students"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<ActionResult<List<StudentDto>>> EligibleStudents(Guid id) => Ok(await manager.EligibleStudentsAsync(id));
    [HttpPost, Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<ActionResult<Guid>> Add(CreateExamDto dto)
    { var id = await manager.CreateAsync(dto); return CreatedAtAction(nameof(GetById), new { id }, id); }
    [HttpPut("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<IActionResult> Update(Guid id, UpdateExamDto dto) { await manager.UpdateAsync(id, dto); return NoContent(); }
    [HttpPut("{id:guid}/students"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<IActionResult> Assign(Guid id, UpdateExamStudentsDto dto) { await manager.AssignAsync(id, dto); return NoContent(); }
    [HttpPost("{id:guid}/publish"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<IActionResult> Publish(Guid id) { await manager.PublishAsync(id); return NoContent(); }
    [HttpPost("{id:guid}/close"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<IActionResult> Close(Guid id) { await manager.CloseAsync(id); return NoContent(); }
    [HttpPost("{id:guid}/cancel"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<IActionResult> Cancel(Guid id) { await manager.CancelAsync(id); return NoContent(); }
    [HttpDelete("{id:guid}"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<IActionResult> Delete(Guid id) { await manager.DeleteAsync(id); return NoContent(); }
    [HttpPost("{id:guid}/start"), Authorize(Roles = "Student")]
    public async Task<ActionResult<StartExamDto>> Start(Guid id) => Ok(await manager.StartAsync(id));
    [HttpGet("{id:guid}/submissions"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<ActionResult<List<SubmissionSummaryDto>>> Submissions(Guid id, [FromQuery] SubmissionStatus? status) => Ok(await manager.SubmissionsAsync(id, status));
}
