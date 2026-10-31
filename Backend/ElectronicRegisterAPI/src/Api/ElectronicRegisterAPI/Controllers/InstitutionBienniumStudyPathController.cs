using ElectronicRegisterAPI.Api.Http;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/biennium-study-paths"), InstitutionSchoolEndpoint, Authorize(Roles = "InstitutionAdmin")]
public sealed class InstitutionBienniumStudyPathController(IInstitutionAcademicCatalogManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int pageNumber = 1, int pageSize = 20)
    {
        InstitutionAcademicHttp.Query(Request);
        return Ok(await manager.ListBienniumStudyPathAsync(pageNumber, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await manager.GetBienniumStudyPathAsync(id, ct);
        if (row is null)
            return NotFound();
        Response.Headers.ETag = $"\"{row.Revision}\"";
        return Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInstitutionBienniumStudyPathDto dto, CancellationToken ct)
    {
        var id = await manager.CreateBienniumStudyPathAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await manager.DeleteBienniumStudyPathAsync(id, ct);
        return NoContent();
    }
}
