using System.Globalization;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/classes"), InstitutionSchoolEndpoint, Authorize(Roles = "InstitutionAdmin")]
public sealed class InstitutionClassController(IInstitutionClassManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InstitutionClassPageDto>> GetAll(
        CancellationToken ct,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? bienniumStudyPathId = null,
        [FromQuery] Guid? courseEditionId = null
    )
    {
        var allowed = new HashSet<string>(new[] { "pageNumber", "pageSize", "bienniumStudyPathId", "courseEditionId" }, StringComparer.OrdinalIgnoreCase);
        if (Request.Query.Keys.Any(k => !allowed.Contains(k)))
            throw new ArgumentException("Filtro non supportato.");
        return Ok(await manager.GetAllAsync(pageNumber, pageSize, bienniumStudyPathId, courseEditionId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InstitutionClassDto>> GetById(Guid id, CancellationToken ct)
    {
        var row = await manager.GetByIdAsync(id, ct);
        if (row is null)
            return NotFound();
        Response.Headers.ETag = $"\"{row.Revision}\"";
        return Ok(row);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Add(CreateInstitutionClassDto dto, CancellationToken ct)
    {
        var id = await manager.AddAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateInstitutionClassDto dto, CancellationToken ct)
    {
        var value = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(value))
            return StatusCode(428, new ProblemDetails { Status = 428, Title = "If-Match richiesto" });
        if (value.Length < 3 || value[0] != '"' || value[^1] != '"' || !long.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var revision))
            throw new ArgumentException("If-Match non valido.");
        await manager.UpdateAsync(id, dto, revision, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await manager.DeleteAsync(id, ct);
        return NoContent();
    }
}
