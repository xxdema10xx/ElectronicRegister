using Microsoft.AspNetCore.Authorization;
namespace ElectronicRegisterAPI.Api.Security;
[AttributeUsage(AttributeTargets.Class, Inherited=false)]
public sealed class InstitutionCourseEndpointAttribute : AuthorizeAttribute
{
    public const string PolicyName="InstitutionCourseAccess";
    public const string RolloutKey="Multitenancy:CourseApi:Enabled";
    public InstitutionCourseEndpointAttribute() => Policy=PolicyName;
    internal static bool Enabled(IConfiguration? configuration) => InstitutionSchoolEndpointAttribute.Enabled(configuration)
        && bool.TryParse(configuration?[RolloutKey],out var enabled) && enabled;
}
