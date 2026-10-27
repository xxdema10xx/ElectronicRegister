using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionSchoolEndpoint, Route("api/institution"), RequestSizeLimit(16384)]
public sealed class InstitutionEmailController(IInstitutionEmailManager manager) : ControllerBase
{
    [HttpGet("email-policies"), Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Policies(CancellationToken ct)=>Ok(await manager.PoliciesAsync(ct));
    [HttpGet("email-policies/{category}"), Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Policy(EmailCategory category,long? revision,CancellationToken ct)=>Ok(await manager.PolicyAsync(category,revision,ct));
    // ExpectedRevision=0 creates a category; subsequent PUTs append immutable versions.
    [HttpPost("email-policies/{category}"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> CreatePolicy(EmailCategory category,EmailPolicyWrite request,CancellationToken ct)
    {if(request.ExpectedRevision!=0)return BadRequest();return Ok(await manager.WritePolicyAsync(category,request,ct));}
    [HttpPut("email-policies/{category}"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> UpdatePolicy(EmailCategory category,EmailPolicyWrite request,CancellationToken ct)
    {if(request.ExpectedRevision<1)return BadRequest();return Ok(await manager.WritePolicyAsync(category,request,ct));}
    [HttpGet("email-policies/{category}/history"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> PolicyHistory(EmailCategory category,CancellationToken ct,int page=1,int size=20)=>Ok(await manager.HistoryAsync(null,category,page,size,ct));
    [HttpPost("email-policies/preview"), Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Preview(EmailPreviewRequest request,CancellationToken ct)=>Ok(await manager.PreviewAsync(request,ct));
    [HttpGet("email-assignments"), Authorize(Roles="InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<IActionResult> Assignments(CancellationToken ct,int page=1,int size=20)=>Ok(await manager.AssignmentsAsync(page,size,ct));
    [HttpGet("email-assignments/{id:guid}"), Authorize(Roles="InstitutionAdmin,Staff,Teacher,Student")]
    public async Task<IActionResult> Assignment(Guid id,CancellationToken ct)=>Ok(await manager.AssignmentAsync(id,ct));
    [HttpPost("email-assignments"), Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> Reserve(EmailReserve request,CancellationToken ct)=>Ok(await manager.ReserveAsync(request,ct));
    [HttpPost("email-assignments/create-person"), Authorize(Roles="InstitutionAdmin,Staff")]
    public async Task<IActionResult> CreatePerson(EmailCreatePerson request,CancellationToken ct)=>Ok(await manager.CreatePersonAsync(request,ct));
    [HttpPost("email-assignments/{id:guid}/override"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Override(Guid id,EmailOverride request,CancellationToken ct)=>Ok(await manager.OverrideAsync(id,request,ct));
    [HttpPost("email-assignments/{id:guid}/retire"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> Retire(Guid id,EmailRetire request,CancellationToken ct)=>Ok(await manager.RetireAsync(id,request,ct));
    [HttpGet("email-assignments/{id:guid}/history"), Authorize(Roles="InstitutionAdmin")]
    public async Task<IActionResult> AssignmentHistory(Guid id,CancellationToken ct,int page=1,int size=20)=>Ok(await manager.HistoryAsync(id,null,page,size,ct));
}
