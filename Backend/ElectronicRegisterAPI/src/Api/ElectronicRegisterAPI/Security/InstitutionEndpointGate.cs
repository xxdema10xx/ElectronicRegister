using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Constants;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;

namespace ElectronicRegisterAPI.Api.Security;

// Applied globally to MVC: future unannotated legacy controllers also fail closed.
internal sealed class InstitutionEndpointGate(ICurrentTenant? current = null, IConfiguration? configuration = null) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;
        var identity = metadata.OfType<InstitutionIdentityEndpointAttribute>().ToArray();
        var school = metadata.OfType<InstitutionSchoolEndpointAttribute>().ToArray();
        var exam = metadata.OfType<InstitutionExamEndpointAttribute>().ToArray();
        var course = metadata.OfType<InstitutionCourseEndpointAttribute>().ToArray();
        var members = metadata.OfType<InstitutionMembershipAdminEndpointAttribute>().ToArray();
        var writes = metadata.OfType<InstitutionMembershipWriteEndpointAttribute>().ToArray();
        // A write marker cannot authorize by itself or combine with other surfaces.
        if (writes.Length > 1 || (writes.Length == 1 && (members.Length != 1 || !HttpMethods.IsPost(context.HttpContext.Request.Method) || !InstitutionMembershipWriteEndpointAttribute.Enabled(configuration))))
        {
            context.Result = Denied();
            return Task.CompletedTask;
        }

        if (members.Length == 1 && !HttpMethods.IsGet(context.HttpContext.Request.Method) && writes.Length != 1)
        {
            context.Result = Denied();
            return Task.CompletedTask;
        }

        var markerCount = identity.Length + school.Length + exam.Length + course.Length + members.Length;
        if (markerCount > 1)
        {
            context.Result = Denied();
            return Task.CompletedTask;
        }

        if (identity.Length == 1)
            return Task.CompletedTask;
        if (markerCount == 1)
        {
            var policies = metadata.OfType<IAuthorizeData>().ToArray();
            var expectedPolicy = members.Length == 1 ? InstitutionMembershipAdminEndpointAttribute.PolicyName : course.Length == 1 ? InstitutionCourseEndpointAttribute.PolicyName : exam.Length == 1 ? InstitutionExamEndpointAttribute.PolicyName : InstitutionSchoolEndpointAttribute.PolicyName;
            var enabled = members.Length == 1 ? InstitutionMembershipAdminEndpointAttribute.Enabled(configuration) : course.Length == 1 ? InstitutionCourseEndpointAttribute.Enabled(configuration) : exam.Length == 1 ? InstitutionExamEndpointAttribute.Enabled(configuration) : InstitutionSchoolEndpointAttribute.Enabled(configuration);
            if (metadata.OfType<IAllowAnonymous>().Any() || policies.Count(p => p.Policy == expectedPolicy) != 1 || policies.Any(p => !string.IsNullOrEmpty(p.AuthenticationSchemes) || (!string.IsNullOrEmpty(p.Policy) && p.Policy != expectedPolicy)) || !enabled || context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                context.Result = Denied();
                return Task.CompletedTask;
            }

            try
            {
                _ = current?.Value ?? throw new UnauthorizedAccessException();
            }
            catch (UnauthorizedAccessException)
            {
                context.Result = Denied();
            }

            return Task.CompletedTask;
        }

        context.Result = Denied();
        return Task.CompletedTask;
    }

    private static ObjectResult Denied() => new(new ProblemDetails { Status = 403, Title = "Non autorizzato" })
    {
        StatusCode = 403
    };
}
