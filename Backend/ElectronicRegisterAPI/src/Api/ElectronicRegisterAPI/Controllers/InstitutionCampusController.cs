using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/campuses"), InstitutionCourseEndpoint, CourseApiErrors]
public sealed class InstitutionCampusController(IInstitutionCourseManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> List(CancellationToken ct, int pageNumber=1, int pageSize=20) =>
        Ok(await manager.CampusesAsync(null,pageNumber,pageSize,ct));
}
