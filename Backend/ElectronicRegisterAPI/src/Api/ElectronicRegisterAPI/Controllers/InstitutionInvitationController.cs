using ElectronicRegisterAPI.Api.Contracts;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Api.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class InviteMemberRequest
{
    [JsonRequired] public Guid EventId { get; init; }
    [JsonRequired] public Guid RecipientUserId { get; init; }
    [JsonRequired] public string[] Roles { get; init; } = [];
    [JsonRequired] public string Reason { get; init; } = "";
    internal InviteMember ToDomain()
    {
        if(Roles is null || Roles.Any(r=>!Enum.GetNames<InstitutionRole>().Contains(r,StringComparer.Ordinal)))throw new ArgumentException();
        return new(EventId,RecipientUserId,Roles.Select(Enum.Parse<InstitutionRole>).ToArray(),Reason);
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class InvitationChangeRequest
{
    [JsonRequired] public Guid EventId { get; init; }
    [JsonRequired] public string Reason { get; init; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class InvitationAcceptanceRequest
{
    [JsonRequired] public Guid InstitutionId { get; init; }
    [JsonRequired] public Guid EventId { get; init; }
    [JsonRequired] public string Challenge { get; init; } = "";
}

[ApiController,Route("api/institution/admin/invitations"),InstitutionMembershipAdminEndpoint,RequestSizeLimit(16384),MembershipApiErrors]
public sealed class InstitutionInvitationController(IInstitutionOnboardingManager manager) : ControllerBase
{
    [HttpPost,InstitutionMembershipWriteEndpoint]
    public async Task<IActionResult> Invite(InviteMemberRequest request,CancellationToken ct)=>Ok(await manager.InviteAsync(request.ToDomain(),ct));
    [HttpPost("{id:guid}/revoke"),InstitutionMembershipWriteEndpoint]
    public async Task<IActionResult> Revoke(Guid id,InvitationChangeRequest request,CancellationToken ct)=>Ok(await manager.RevokeAsync(new(request.EventId,id,request.Reason),ct));
    [HttpPost("{id:guid}/reissue"),InstitutionMembershipWriteEndpoint]
    public async Task<IActionResult> Reissue(Guid id,InvitationChangeRequest request,CancellationToken ct)=>Ok(await manager.ReissueAsync(new(request.EventId,id,request.Reason),ct));
}

[ApiController,Route("api/Auth/invitations"),InstitutionIdentityEndpoint,RequestSizeLimit(16384),MembershipApiErrors]
public sealed class InvitationAcceptanceController(IInstitutionOnboardingManager manager) : ControllerBase
{
    [HttpPost("{id:guid}/accept"),Authorize(Policy=ElectronicRegisterAPI.Infrastructure.Security.InstitutionTokenContract.SelectionScheme)]
    public async Task<IActionResult> Accept(Guid id,InvitationAcceptanceRequest request,CancellationToken ct)
    {
        var header=Request.Headers.Authorization.ToString();
        if(!header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))return Unauthorized();
        return Ok(await manager.AcceptAsync(header[7..],new(request.InstitutionId,id,request.EventId,request.Challenge),ct));
    }
}
