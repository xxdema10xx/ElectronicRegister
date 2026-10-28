using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicRegisterAPI.Api.Controllers;

[ApiController, InstitutionIdentityEndpoint, Route("api/Auth")]
public sealed class InstitutionIdentityController(IInstitutionAuthManager manager, ICurrentTenant tenant) : ControllerBase
{
    [HttpPost("identity/login")]
    public async Task<ActionResult<IdentityTicketDto>> Login(LoginDto input)
    {
        var result = await manager.LoginAsync(input);
        return result is null ? Problem(statusCode: 401, title: "Credenziali non valide") : Ok(result);
    }

    [HttpPost("identity/microsoft-login")]
    public async Task<ActionResult<IdentityTicketDto>> MicrosoftLogin(MicrosoftLoginDto input)
    {
        var result = await manager.MicrosoftLoginAsync(input);
        return result is null ? Problem(statusCode: 401, title: "Identita non autorizzata") : Ok(result);
    }

    [HttpGet("institutions"), Authorize(Policy = InstitutionTokenContract.SelectionScheme)]
    public async Task<ActionResult<IReadOnlyList<InstitutionChoiceDto>>> Institutions(CancellationToken cancellationToken) => Ok(await manager.InstitutionsAsync(InstitutionAuthentication.GuidClaim(User, "sub"), cancellationToken));
    [HttpPost("select-institution"), Authorize(Policy = InstitutionTokenContract.SelectionScheme)]
    public async Task<ActionResult<InstitutionAccessDto>> Select(SelectInstitutionDto input, CancellationToken cancellationToken) => Ok(await manager.SelectAsync(InstitutionAuthentication.GuidClaim(User, "sub"), input, cancellationToken));
    [HttpGet("context"), Authorize(Policy = InstitutionTokenContract.AccessScheme)]
    public ActionResult<TenantContextDto> Context() => Ok(new TenantContextDto(tenant.Value.UserId, tenant.Value.InstitutionId, tenant.Value.MembershipId, tenant.Value.Roles.Select(r => r.ToString()).Order().ToArray()));
}
