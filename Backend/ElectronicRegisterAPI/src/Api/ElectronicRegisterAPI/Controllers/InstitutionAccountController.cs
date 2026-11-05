using ElectronicRegisterAPI.Api.Contracts;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

// Institution-scoped account creation (InstitutionAdmin any role; Staff Student/Teacher).
// Off unless Multitenancy:SchoolApi and Multitenancy:MembershipWriteApi are enabled
// (in Production both also require Deployment:RolloutApproved).
[ApiController, Route("api/institution/accounts"), InstitutionSchoolEndpoint, RequestSizeLimit(16384), MembershipApiErrors]
public sealed class InstitutionAccountController(IInstitutionAccountManager manager) : ControllerBase
{
    [HttpPost, Authorize(Roles = "InstitutionAdmin,Staff")]
    [InstitutionPermission(InstitutionPermission.UserCreate)]
    public async Task<IActionResult> Create(CreateInstitutionAccountRequest request, [FromServices] IConfiguration configuration, CancellationToken ct)
    {
        if (!InstitutionMembershipWriteEndpointAttribute.Enabled(configuration))
            return NotFound();
        var result = await manager.CreateAsync(request.ToDomain(), ct);
        // The temporary password is shown once: never cache this response anywhere.
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return result.Replayed ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }
}
