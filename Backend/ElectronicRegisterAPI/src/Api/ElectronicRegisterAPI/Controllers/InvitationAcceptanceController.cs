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

[ApiController, Route("api/Auth/invitations"), InstitutionIdentityEndpoint, RequestSizeLimit(16384), MembershipApiErrors]
public sealed class InvitationAcceptanceController(IInstitutionOnboardingManager manager) : ControllerBase
{
    [HttpPost("{id:guid}/accept"), Authorize(Policy = ElectronicRegisterAPI.Infrastructure.Security.InstitutionTokenContract.SelectionScheme)]
    public async Task<IActionResult> Accept(Guid id, InvitationAcceptanceRequest request, CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();
        return Ok(await manager.AcceptAsync(header[7..], new(request.InstitutionId, id, request.EventId, request.Challenge), ct));
    }
}
