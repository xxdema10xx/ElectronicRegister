using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/enrollments"), InstitutionCourseEndpoint, CourseApiErrors]
public sealed class InstitutionEnrollmentController(IInstitutionCourseManager manager) : ControllerBase
{
    [HttpGet("{id:guid}"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await manager.EnrollmentAsync(id,ct));
    [HttpGet, Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> List([FromQuery] Guid studentId, CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.EnrollmentsAsync(studentId,null,pageNumber,pageSize,ct));
    [HttpPost, Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Create(CreateStudentEnrollmentDto dto, CancellationToken ct)
    { var id=await manager.CreateEnrollmentAsync(dto,ct); return CreatedAtAction(nameof(Get),new { id },id); }
    [HttpGet("me"), Authorize(Roles="Student")]
    public async Task<IActionResult> Me(CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.OwnEnrollmentsAsync(pageNumber,pageSize,ct));
    [HttpPost("{id:guid}/transition"), Authorize(Roles="InstitutionAdmin"), RequestSizeLimit(16384)]
    public async Task<IActionResult> Transition(Guid id, TransitionEnrollmentDto dto, CancellationToken ct) =>
        Ok(await manager.TransitionAsync(id,dto,ct));
    [HttpPut("{id:guid}/completion-date"), Authorize(Roles="InstitutionAdmin"), RequestSizeLimit(16384)]
    public async Task<IActionResult> CorrectCompletion(Guid id, CorrectEnrollmentCompletionDto dto, CancellationToken ct) =>
        Ok(await manager.CorrectCompletionAsync(id,dto,ct));
    [HttpGet("{id:guid}/history"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.HistoryAsync(id,pageNumber,pageSize,ct));
}
