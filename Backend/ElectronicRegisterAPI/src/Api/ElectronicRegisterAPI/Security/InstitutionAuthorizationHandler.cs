using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace ElectronicRegisterAPI.Api.Security;

internal sealed class InstitutionAuthorizationHandler(IInstitutionAuthManager manager, CurrentTenant current, IHttpContextAccessor accessor) : AuthorizationHandler<InstitutionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, InstitutionRequirement requirement)
    {
        var ct = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (context.User.Identity?.IsAuthenticated != true)
            return;
        if (requirement.Selection)
        {
            if (await manager.IdentityExistsAsync(InstitutionAuthentication.GuidClaim(context.User, "sub")))
                context.Succeed(requirement);
        }
        else
        {
            var tenant = await manager.AuthorizeAsync(InstitutionAuthentication.ParseTenant(context.User), ct);
            if (tenant is not null)
            {
                current.Set(tenant);
                context.Succeed(requirement);
            }
        }
    }
}
