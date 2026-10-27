using System.Text.Json;
using ElectronicRegisterAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ElectronicRegisterAPI.Api.Security;

/// <summary>Staff writes fail closed on unknown input fields without changing historical Teacher contracts.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class StaffBodyContractAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var roles = context.HttpContext.User.FindAll(InstitutionTokenContract.Role).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        var request = context.HttpContext.Request;
        if (roles.Contains("Staff") && !roles.Contains("InstitutionAdmin") && request.Method is "POST" or "PUT" &&
            context.ActionDescriptor is ControllerActionDescriptor action &&
            action.Parameters.OfType<ControllerParameterDescriptor>().FirstOrDefault(p => p.BindingInfo?.BindingSource == BindingSource.Body) is { } body)
        {
            request.EnableBuffering();
            try
            {
                using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
                var fields = body.ParameterInfo.ParameterType.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Any(p => !fields.Contains(p.Name)))
                { context.Result = new BadRequestResult(); return; }
            }
            catch (JsonException) { context.Result = new BadRequestResult(); return; }
            finally { request.Body.Position = 0; }
        }
        await next();
    }
}
