using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionIdentityEndpoint, Route("api/identity/accounts")]
public sealed class IdentityRegistrationController(IIdentityAccountManager manager) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Guid>> Register(RegisterIdentityAccountDto input, CancellationToken ct)
    {
        var id = await manager.RegisterAsync(input, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }
}
