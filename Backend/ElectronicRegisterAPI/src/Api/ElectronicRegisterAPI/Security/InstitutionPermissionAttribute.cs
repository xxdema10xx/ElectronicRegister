using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ElectronicRegisterAPI.Api.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class InstitutionPermissionAttribute(InstitutionPermission permission) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenant = context.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>().Value;
        InstitutionPermissions.Require(tenant, permission);
        await next();
    }
}
