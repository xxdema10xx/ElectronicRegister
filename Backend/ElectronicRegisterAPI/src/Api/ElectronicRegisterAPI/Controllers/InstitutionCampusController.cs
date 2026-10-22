using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;
[ApiController, Route("api/institution/campuses"), InstitutionCourseEndpoint, Authorize(Roles = "InstitutionAdmin")]
public sealed class InstitutionCampusController(IInstitutionCampusManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int pageNumber = 1, int pageSize = 20, bool? isActive = null)
    {
        var allowed = new HashSet<string>(new[] { "pageNumber", "pageSize", "isActive" }, StringComparer.OrdinalIgnoreCase);
        if (Request.Query.Keys.Any(k => !allowed.Contains(k)))
            throw new ArgumentException("Filtro non supportato.");
        var page = await manager.ListAsync(pageNumber, pageSize, isActive, ct);
        Response.Headers["X-Total-Count"] = page.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Ok(page.Items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await manager.GetAsync(id, ct);
        if (row is null)
            return NotFound();
        Response.Headers.ETag = $"\"{row.Revision}\"";
        return Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInstitutionCampusDto dto, CancellationToken ct)
    {
        var id = await manager.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateInstitutionCampusDto dto, CancellationToken ct)
    {
        var revision = InstitutionAcademicHttp.Revision(Request);
        if (revision is null)
            return StatusCode(428, new ProblemDetails { Status = 428, Title = "If-Match richiesto" });
        await manager.UpdateAsync(id, dto, revision.Value, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await manager.DeleteAsync(id, ct);
        return NoContent();
    }
}
