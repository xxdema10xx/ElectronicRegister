using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/course-editions"), InstitutionCourseEndpoint, CourseApiErrors]
public sealed class InstitutionCourseEditionController(IInstitutionCourseManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> List(Guid? studyPathId, CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.EditionsAsync(studyPathId,pageNumber,pageSize,ct));
    [HttpGet("{id:guid}"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await manager.EditionAsync(id,null,ct));
    [HttpGet("by-code/{code}"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> ByCode(string code, CancellationToken ct) => Ok(await manager.EditionAsync(null,code,ct));
    [HttpPost, Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Create(CreateCourseEditionDto dto, CancellationToken ct)
    { var id=await manager.CreateEditionAsync(dto,ct); return CreatedAtAction(nameof(Get),new { id },id); }
    [HttpPut("{id:guid}"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateCourseEditionDto dto, CancellationToken ct)
    { await manager.UpdateEditionAsync(id,dto,ct); return NoContent(); }
    [HttpGet("{id:guid}/campuses"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Campuses(Guid id, CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.CampusesAsync(id,pageNumber,pageSize,ct));
    [HttpPost("{id:guid}/campuses"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> LinkCampus(Guid id, LinkEditionCampusDto dto, CancellationToken ct)
    { await manager.LinkCampusAsync(id,dto.CampusId,ct); return CreatedAtAction(nameof(Campuses),new { id },null); }
    [HttpGet("{id:guid}/classes"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Classes(Guid id, CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.ClassesAsync(id,pageNumber,pageSize,ct));
    [HttpPost("{editionId:guid}/classes/{classId:guid}"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> AssignClass(Guid editionId, Guid classId, CancellationToken ct)
    { await manager.AssignClassAsync(editionId,classId,ct); return NoContent(); }
    [HttpGet("{id:guid}/enrollments"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Enrollments(Guid id, CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.EnrollmentsAsync(null,id,pageNumber,pageSize,ct));
    [HttpGet("{id:guid}/audit"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Audit(Guid id, CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.EditionAuditAsync(id,pageNumber,pageSize,ct));
}
