using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Mvc;
namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController,Route("api/institution/admin/audit"),InstitutionMembershipAdminEndpoint,MembershipApiErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class InstitutionMembershipAuditController(IMembershipAuditManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct,int page=1,int size=20,Guid? target=null,string? action=null)=>
        Ok(await manager.ListAsync(page,size,target,action??(Request.Query.ContainsKey("action")?"":null),ct));
    [HttpGet("{eventId:guid}")]
    public async Task<IActionResult> Get(Guid eventId,CancellationToken ct)=>Ok(await manager.GetAsync(eventId,ct));
}
