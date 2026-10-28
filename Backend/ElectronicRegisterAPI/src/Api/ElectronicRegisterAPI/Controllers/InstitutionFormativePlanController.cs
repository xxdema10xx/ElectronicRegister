using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionSchoolEndpoint, ElectronicRegisterAPI.Api.ExceptionHandling.FormativePlanErrors, Route("api/institution"), RequestSizeLimit(2*1024*1024)]
public sealed class InstitutionFormativePlanController(IInstitutionFormativePlanManager manager) : ControllerBase
{
    [HttpGet("formative-plans"),Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> List(CancellationToken ct,int page=1,int size=20)=>Ok(await manager.ListAsync(page,size,ct));
    [HttpGet("formative-plans/{id:guid}"),Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await manager.GetAsync(id,ct));
    [HttpPost("formative-plans"),Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Create(CreateFormativePlan request,CancellationToken ct)=>Ok(await manager.CreateAsync(request,ct));
    [HttpPut("formative-plans/{id:guid}"),Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Rename(Guid id,RenameFormativePlan request,CancellationToken ct)=>Ok(await manager.RenameAsync(id,request,ct));
    [HttpGet("formative-plans/{id:guid}/versions"),Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Versions(Guid id,CancellationToken ct,int page=1,int size=20)=>Ok(await manager.VersionsAsync(id,page,size,ct));
    [HttpPost("formative-plans/{id:guid}/versions"),Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> CreateVersion(Guid id,CreateFormativeVersion request,CancellationToken ct)=>Ok(await manager.CreateVersionAsync(id,request,ct));
    [HttpGet("formative-plan-versions/{id:guid}"),Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Version(Guid id,CancellationToken ct)=>Ok(await manager.VersionAsync(id,ct));
    [HttpPut("formative-plan-versions/{id:guid}"),Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Replace(Guid id,ReplaceFormativeDraft request,CancellationToken ct)=>Ok(await manager.ReplaceAsync(id,request,ct));
    [HttpPost("formative-plan-versions/{id:guid}/clone"),Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Clone(Guid id,FormativeCommand request,CancellationToken ct)=>Ok(await manager.CloneAsync(id,request,ct));
    [HttpPost("formative-plan-versions/{id:guid}/publish"),Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Publish(Guid id,FormativeCommand request,CancellationToken ct)=>Ok(await manager.PublishAsync(id,request,ct));
    [HttpPost("formative-plan-versions/{id:guid}/validate"),Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Validate(Guid id,CancellationToken ct)=>Ok(await manager.ValidateAsync(id,ct));
    [HttpGet("formative-plan-versions/{id:guid}/hour-summary"),Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Summary(Guid id,CancellationToken ct)=>Ok(await manager.ValidateAsync(id,ct));
    [HttpPost("course-editions/{id:guid}/formative-plan"),Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Bind(Guid id,BindFormativeVersion request,CancellationToken ct)=>Ok(await manager.BindAsync(id,request,ct));
    [HttpGet("course-editions/{id:guid}/formative-plan"),Authorize(Roles="InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<IActionResult> EditionPlan(Guid id,CancellationToken ct)=>Ok(await manager.EditionPlanAsync(id,ct));
    [HttpGet("course-editions/{id:guid}/formative-plan/modules/{moduleId:guid}"),Authorize(Roles="InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<IActionResult> EditionModule(Guid id,Guid moduleId,CancellationToken ct)
    {
        var version=await manager.EditionPlanAsync(id,ct);
        return Ok(version.Units.SelectMany(u=>u.Modules).SingleOrDefault(m=>m.Id==moduleId)??throw new KeyNotFoundException());
    }
    [HttpGet("formative-plans/{id:guid}/history"),Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> History(Guid id,CancellationToken ct,int page=1,int size=20)=>Ok(await manager.HistoryAsync(id,page,size,ct));
}
