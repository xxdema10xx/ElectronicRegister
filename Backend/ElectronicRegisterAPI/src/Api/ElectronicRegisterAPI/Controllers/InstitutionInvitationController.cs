using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Api.Contracts;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, Route("api/institution/admin/invitations"), InstitutionMembershipAdminEndpoint, RequestSizeLimit(16384), MembershipApiErrors]
public sealed class InstitutionInvitationController(IInstitutionOnboardingManager manager) : ControllerBase
{
    [HttpPost, InstitutionMembershipWriteEndpoint]
    public async Task<IActionResult> Invite(InviteMemberRequest request, CancellationToken ct) => Ok(await manager.InviteAsync(request.ToDomain(), ct));
    [HttpPost("{id:guid}/revoke"), InstitutionMembershipWriteEndpoint]
    public async Task<IActionResult> Revoke(Guid id, InvitationChangeRequest request, CancellationToken ct) => Ok(await manager.RevokeAsync(new(request.EventId, id, request.Reason), ct));
    [HttpPost("{id:guid}/reissue"), InstitutionMembershipWriteEndpoint]
    public async Task<IActionResult> Reissue(Guid id, InvitationChangeRequest request, CancellationToken ct) => Ok(await manager.ReissueAsync(new(request.EventId, id, request.Reason), ct));
}
