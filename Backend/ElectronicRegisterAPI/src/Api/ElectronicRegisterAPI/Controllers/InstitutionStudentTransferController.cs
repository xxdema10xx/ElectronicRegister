using ElectronicRegisterAPI.Api.Http;
using ElectronicRegisterAPI.Api.Filters;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ElectronicRegisterAPI.Api.Controllers;

// A single existing School marker satisfies LegacyEndpointGate. Course rollout is
// an additional fail-closed filter, not an unsupported combination of IAM policies.
[ApiController, Route("api/institution/students"), InstitutionSchoolEndpoint, CourseApiErrors, StudentTransferErrors, Authorize(Roles = "InstitutionAdmin")]
public sealed class InstitutionStudentTransferController(IInstitutionStudentTransferManager manager) : ControllerBase
{
    [HttpPost("{id:guid}/transfer-class"), RequestSizeLimit(16384), RequireTransferCourseApi]
    public async Task<IActionResult> Transfer(Guid id, TransferStudentClassDto dto, CancellationToken ct)
    {
        if (Request.Query.Count != 0)
            throw new ArgumentException("Filtro non supportato.");
        return Ok(await manager.TransferAsync(id, dto, ct));
    }

    [HttpGet("{id:guid}/class-history")]
    public async Task<IActionResult> History(
        Guid id,
        CancellationToken ct,
        int pageNumber = 1,
        int pageSize = 20
    )
    {
        InstitutionAcademicHttp.Query(Request);
        return Ok(await manager.HistoryAsync(id, pageNumber, pageSize, ct));
    }
}
