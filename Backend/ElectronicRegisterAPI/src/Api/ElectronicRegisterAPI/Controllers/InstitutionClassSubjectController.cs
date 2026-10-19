using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionExamEndpoint, Route("api/institution/class-subjects")]
public sealed class InstitutionClassSubjectController(IInstitutionExamManager manager) : ControllerBase
{
    [HttpGet, Authorize(Roles = "InstitutionAdmin,Teacher")]
    public async Task<ActionResult<List<ClassSubjectChoiceDto>>> GetAll() => Ok(await manager.ClassSubjectsAsync());
}
