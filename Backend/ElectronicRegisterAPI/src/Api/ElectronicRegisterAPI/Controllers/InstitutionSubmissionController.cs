using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionExamEndpoint, Route("api/institution/submissions")]
public sealed class InstitutionSubmissionController(IInstitutionExamManager manager) : ControllerBase
{
    [HttpGet("{id:guid}/session"), Authorize(Roles = "Student")]
    public async Task<ActionResult<SubmissionSessionDto>> Session(Guid id) => Ok(await manager.SessionAsync(id));
    [HttpPut("{id:guid}/answers"), Authorize(Roles = "Student")]
    public async Task<ActionResult<SaveAnswersResponseDto>> Save(Guid id, SaveExamAnswersDto dto) => Ok(await manager.SaveAsync(id, dto));
    [HttpPost("{id:guid}/submit"), Authorize(Roles = "Student")]
    public async Task<ActionResult<SubmissionResultDto>> Submit(Guid id, SubmitExamDto dto) => Ok(await manager.SubmitAsync(id, dto));
    [HttpPost("{id:guid}/submit-expired"), Authorize(Roles = "Student")]
    public async Task<ActionResult<SubmissionResultDto>> SubmitExpired(Guid id)
    {
        if (Request.ContentLength > 0 || Request.Headers.ContainsKey("Transfer-Encoding")) return BadRequest();
        return Ok(await manager.SubmitExpiredAsync(id));
    }
    [HttpPost("{id:guid}/violations"), Authorize(Roles = "Student"), RequestSizeLimit(4096)]
    public async Task<ActionResult<ViolationReceiptDto>> ReportViolation(Guid id, InstitutionViolationDto dto) => Ok(await manager.ReportViolationAsync(id, dto));
    [HttpGet("{id:guid}/violations"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<List<ViolationEventDto>>> Violations(Guid id) => Ok(await manager.ViolationsAsync(id));
    [HttpGet("{id:guid}/review"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher")]
    public async Task<ActionResult<SubmissionReviewDto>> Review(Guid id) => Ok(await manager.ReviewAsync(id));
    [HttpPut("{id:guid}/review"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<IActionResult> Review(Guid id, ReviewOpenAnswersDto dto) { await manager.ReviewAsync(id, dto); return NoContent(); }
    [HttpPost("{id:guid}/finalize"), Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<IActionResult> FinalizeSubmission(Guid id, FinalizeSubmissionDto dto) { await manager.FinalizeAsync(id, dto); return NoContent(); }
    [HttpGet("{id:guid}/result"), Authorize(Roles = "InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<ActionResult<SubmissionResultDto>> Result(Guid id) => Ok(await manager.ResultAsync(id));
}
