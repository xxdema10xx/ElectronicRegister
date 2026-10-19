using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Constants;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;

namespace ElectronicRegisterAPI.Api.Security;

// Applied globally to MVC: future unannotated legacy controllers also fail closed.
internal sealed class LegacyEndpointGate(ILegacyAccess legacy, IOptions<LegacyIdentityOptions> options,
    ICurrentTenant? current = null, IConfiguration? configuration = null) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;
        var identity = metadata.OfType<InstitutionIdentityEndpointAttribute>().ToArray();
        var school = metadata.OfType<InstitutionSchoolEndpointAttribute>().ToArray();
        var exam = metadata.OfType<InstitutionExamEndpointAttribute>().ToArray();
        var course = metadata.OfType<InstitutionCourseEndpointAttribute>().ToArray();
        var markerCount=identity.Length+school.Length+exam.Length+course.Length;
        if(markerCount>1) { context.Result=Denied(); return; }
        if(identity.Length==1) return;
        if(markerCount==1)
        {
            var policies=metadata.OfType<IAuthorizeData>().ToArray();
            var expectedPolicy=course.Length==1 ? InstitutionCourseEndpointAttribute.PolicyName :
                exam.Length==1 ? InstitutionExamEndpointAttribute.PolicyName : InstitutionSchoolEndpointAttribute.PolicyName;
            var enabled=course.Length==1 ? InstitutionCourseEndpointAttribute.Enabled(configuration) :
                exam.Length==1 ? InstitutionExamEndpointAttribute.Enabled(configuration) : InstitutionSchoolEndpointAttribute.Enabled(configuration);
            if(metadata.OfType<IAllowAnonymous>().Any() ||
                policies.Count(p=>p.Policy==expectedPolicy)!=1 ||
                policies.Any(p=>!string.IsNullOrEmpty(p.AuthenticationSchemes) ||
                    (!string.IsNullOrEmpty(p.Policy) && p.Policy!=expectedPolicy)) ||
                !enabled || context.HttpContext.User.Identity?.IsAuthenticated!=true)
            { context.Result=Denied(); return; }
            try { _=current?.Value ?? throw new UnauthorizedAccessException(); }
            catch(UnauthorizedAccessException) { context.Result=Denied(); }
            return;
        }
        var http = context.HttpContext;
        var isLogin = context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action &&
            action.ControllerTypeInfo.AsType() == typeof(Controllers.AuthController) && action.ActionName is "Login" or "MicrosoftLogin";
        if (isLogin)
        {
            if (!options.Value.Enabled || options.Value.InstitutionId == Guid.Empty)
                context.Result = Denied();
            return; // Account allowlist checked by AuthManager after credentials, before issuing a token.
        }
        var user = http.User;
        if (user.Identity?.IsAuthenticated != true) { context.Result = new UnauthorizedResult(); return; }
        var ids = user.FindAll("sub").Concat(user.FindAll(ClaimTypes.NameIdentifier)).ToArray();
        var roles = user.FindAll(ClaimTypes.Role).ToArray();
        if (user.HasClaim(c => c.Type is "institution_id" or "membership_id" or "purpose") || ids.Length != 1 ||
            !Guid.TryParse(ids[0].Value, out var userId) || roles.Length != 1 ||
            !Enum.TryParse<UserRole>(roles[0].Value, true, out var role) || !Enum.IsDefined(role) ||
            !Profile(user, CustomClaimTypes.StudentId, out var student) || !Profile(user, CustomClaimTypes.TeacherId, out var teacher) ||
            !await legacy.IsTokenAllowedAsync(new ClaimsContext(userId, role, student, teacher), http.RequestAborted))
            context.Result = Denied();
    }
    private static bool Profile(ClaimsPrincipal principal, string type, out Guid? id)
    {
        var values = principal.FindAll(type).ToArray();
        id = null;
        if (values.Length == 0) return true;
        if (values.Length != 1 || !Guid.TryParse(values[0].Value, out var value) || value == Guid.Empty) return false;
        id = value;
        return true;
    }
    private static ObjectResult Denied() => new(new ProblemDetails { Status = 403, Title = "Non autorizzato" }) { StatusCode = 403 };
}
