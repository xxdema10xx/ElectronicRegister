using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ElectronicRegisterAPI.Api.Controllers;

namespace ElectronicRegisterAPI.Api.Filters;

public sealed class RequireTransferCourseApiFilter(IConfiguration config) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!bool.TryParse(config[InstitutionCourseEndpointAttribute.RolloutKey], out var enabled) || !enabled)
            context.Result = new ObjectResult(new ProblemDetails { Status = 403, Title = "Non autorizzato" })
            {
                StatusCode = 403
            };
        return Task.CompletedTask;
    }
}
