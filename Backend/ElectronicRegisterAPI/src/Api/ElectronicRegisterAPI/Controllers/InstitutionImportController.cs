using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using System.Text.Json;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionSchoolEndpoint, Authorize(Roles = "InstitutionAdmin,Staff"), Route("api/institution/imports"), RequestSizeLimit(256 * 1024)]
public sealed class InstitutionImportController(IInstitutionImportManager manager) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 16
    };
    [HttpGet("templates/students.csv")]
    public async Task<IActionResult> StudentTemplate(CancellationToken ct) => File(await manager.TemplateAsync(ImportCategory.Student, ct), "text/csv; charset=utf-8", "students.csv");
    [HttpGet("templates/teachers.csv")]
    public async Task<IActionResult> TeacherTemplate(CancellationToken ct) => File(await manager.TemplateAsync(ImportCategory.Teacher, ct), "text/csv; charset=utf-8", "teachers.csv");
    [HttpPost("preview"), Consumes("multipart/form-data"), RequestSizeLimit(64 * 1024 * 1024 + 262144), RequestFormLimits(MultipartBodyLengthLimit = 64 * 1024 * 1024 + 262144), EnableRateLimiting("InstitutionImportUpload")]
    public async Task<IActionResult> Preview(
        [FromForm] IFormFile file,
        [FromForm] string settings,
        CancellationToken ct,
        [FromForm] string? parsing = null
    )
    {
        if (Request.Form.Files.Count != 1 || Request.Form.Any(p => p.Key is not ("settings" or "parsing")) || settings.Length > 262144 || parsing?.Length > 2048)
            return BadRequest();
        ImportPreviewSettings options;
        ImportParsingOptions parser;
        try
        {
            options = JsonSerializer.Deserialize<ImportPreviewSettings>(settings, JsonOptions) ?? throw new JsonException();
            parser = parsing is null ? new() : JsonSerializer.Deserialize<ImportParsingOptions>(parsing, JsonOptions) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        await using var stream = file.OpenReadStream();
        var result = await manager.PreviewAsync(stream, file.FileName, parser, options, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1, int size = 20) => Ok(await manager.ListAsync(page, size, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await manager.GetAsync(id, ct));
    [HttpGet("{id:guid}/rows")]
    public async Task<IActionResult> Rows(
        Guid id,
        CancellationToken ct,
        int page = 1,
        int size = 20
    ) => Ok(await manager.RowsAsync(id, page, size, ct));
    [HttpPut("{id:guid}/mapping")]
    public async Task<IActionResult> Remap(Guid id, ImportRemap request, CancellationToken ct) => Ok(await manager.RemapAsync(id, request, ct));
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, ImportConfirmation request, CancellationToken ct) => Ok(await manager.ConfirmAsync(id, request, ct));
    [HttpGet("{id:guid}/result")]
    public async Task<IActionResult> Result(Guid id, CancellationToken ct) => Ok(await manager.ResultAsync(id, ct));
}
