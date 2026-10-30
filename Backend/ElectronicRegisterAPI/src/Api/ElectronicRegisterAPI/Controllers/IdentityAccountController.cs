using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionIdentityEndpoint, Route("api/identity/account")]
[Authorize(Policy = InstitutionTokenContract.SelectionScheme)]
public sealed class IdentityAccountController(IIdentityAccountManager manager) : ControllerBase
{
    private Guid UserId => InstitutionAuthentication.GuidClaim(User, "sub");

    [HttpGet]
    public async Task<ActionResult<IdentityAccountDto>> Get(CancellationToken ct) => Ok(await manager.GetAsync(UserId, ct));
    [HttpPut]
    public async Task<IActionResult> Update(UpdateIdentityAccountDto input, CancellationToken ct)
    {
        await manager.UpdateAsync(UserId, InstitutionAuthentication.IdentityGeneration(User), input, ct);
        return NoContent();
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangeIdentityPasswordDto input, CancellationToken ct)
    {
        await manager.ChangePasswordAsync(UserId, InstitutionAuthentication.IdentityGeneration(User), input, ct);
        return NoContent();
    }
}
