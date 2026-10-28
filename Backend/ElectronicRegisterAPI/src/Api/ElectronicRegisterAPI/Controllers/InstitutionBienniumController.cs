using ElectronicRegisterAPI.Api.Http;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/biennia"), InstitutionSchoolEndpoint, Authorize(Roles = "InstitutionAdmin")]
public sealed class InstitutionBienniumController(IInstitutionAcademicCatalogManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int pageNumber = 1, int pageSize = 20)
    {
        InstitutionAcademicHttp.Query(Request);
        return Ok(await manager.ListBienniumAsync(pageNumber, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await manager.GetBienniumAsync(id, ct);
        if (row is null)
            return NotFound();
        Response.Headers.ETag = $"\"{row.Revision}\"";
        return Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInstitutionBienniumDto dto, CancellationToken ct)
    {
        var id = await manager.CreateBienniumAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateInstitutionBienniumDto dto, CancellationToken ct)
    {
        var revision = InstitutionAcademicHttp.Revision(Request);
        if (revision is null)
            return StatusCode(428, new ProblemDetails { Status = 428, Title = "If-Match richiesto" });
        await manager.UpdateBienniumAsync(id, dto, revision.Value, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await manager.DeleteBienniumAsync(id, ct);
        return NoContent();
    }
}
