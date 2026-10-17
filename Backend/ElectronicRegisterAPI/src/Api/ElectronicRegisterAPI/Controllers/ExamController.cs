using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExamController : ApiControllerBase
{
    private readonly IExamManager _examManager;
    private readonly IExamSubmissionManager _submissionManager;

    public ExamController(IExamManager examManager, IExamSubmissionManager submissionManager)
    {
        _examManager = examManager;
        _submissionManager = submissionManager;
    }

    [HttpGet]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<List<ExamSummaryDto>>> GetAll()
    {
        var items = await _examManager.GetAllAsync(CurrentCaller());
        return Ok(items);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<ExamDetailDto>> GetById(Guid id)
    {
        var dto = await _examManager.GetByIdAsync(id, CurrentCaller());
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("assigned")]
    [Authorize(Roles = "student")]
    public async Task<ActionResult<List<AssignedExamDto>>> GetAssigned()
    {
        var items = await _examManager.GetAssignedAsync(CurrentCaller());
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Add(CreateExamDto dto)
    {
        var id = await _examManager.AddAsync(dto, CurrentCaller());
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Update(Guid id, UpdateExamDto dto)
    {
        await _examManager.UpdateAsync(id, dto, CurrentCaller());
        return NoContent();
    }

    [HttpPut("{id}/students")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> UpdateStudents(Guid id, UpdateExamStudentsDto dto)
    {
        await _examManager.UpdateStudentsAsync(id, dto, CurrentCaller());
        return NoContent();
    }

    [HttpPost("{id}/publish")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Publish(Guid id)
    {
        await _examManager.PublishAsync(id, CurrentCaller());
        return NoContent();
    }

    [HttpPost("{id}/close")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Close(Guid id)
    {
        await _examManager.CloseAsync(id, CurrentCaller());
        return NoContent();
    }

    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Cancel(Guid id)
    {
        await _examManager.CancelAsync(id, CurrentCaller());
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await _examManager.DeleteAsync(id, CurrentCaller());
        return NoContent();
    }

    [HttpPost("{id}/start")]
    [Authorize(Roles = "student")]
    public async Task<ActionResult<StartExamDto>> Start(Guid id)
    {
        var dto = await _examManager.StartAsync(id, CurrentCaller());
        return Ok(dto);
    }

    [HttpPost("{id}/answers/save")]
    [Authorize(Roles = "student")]
    public async Task<ActionResult<SaveAnswersResponseDto>> SaveAnswers(Guid id, SaveExamAnswersDto dto)
    {
        var res = await _examManager.SaveAnswersAsync(id, dto, CurrentCaller());
        return Ok(res);
    }

    [HttpPost("{id}/submit")]
    [Authorize(Roles = "student")]
    public async Task<ActionResult<SubmissionResultDto>> Submit(Guid id, SubmitExamDto dto)
    {
        var res = await _examManager.SubmitAsync(id, dto, CurrentCaller());
        return Ok(res);
    }

    [HttpGet("{examId}/submissions")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<List<SubmissionSummaryDto>>> GetSubmissions(Guid examId, [FromQuery] SubmissionStatus? status)
    {
        return Ok(await _submissionManager.GetByExamAsync(examId, status, CurrentCaller()));
    }

    [HttpGet("submission/{id}/review")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult<SubmissionReviewDto>> GetReview(Guid id)
    {
        return Ok(await _submissionManager.GetByIdAsync(id, CurrentCaller()));
    }

    [HttpPut("submission/{id}/review")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Review(Guid id, ReviewOpenAnswersDto dto)
    {
        await _submissionManager.ReviewAsync(id, dto, CurrentCaller());
        return NoContent();
    }

    [HttpPost("submission/{id}/finalize")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> FinalizeSubmission(Guid id, FinalizeSubmissionDto dto)
    {
        await _submissionManager.FinalizeAsync(id, dto, CurrentCaller());
        return NoContent();
    }

    [HttpGet("submission/{id}/result")]
    [Authorize(Roles = "teacher,admin,student")]
    public async Task<ActionResult<SubmissionResultDto>> GetResult(Guid id)
    {
        var res = await _examManager.GetResultAsync(id, CurrentCaller());
        return Ok(res);
    }
}
