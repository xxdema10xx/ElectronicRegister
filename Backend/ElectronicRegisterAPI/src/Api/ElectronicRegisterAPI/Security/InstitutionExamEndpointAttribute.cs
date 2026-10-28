using Microsoft.AspNetCore.Authorization;

namespace ElectronicRegisterAPI.Api.Security;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class InstitutionExamEndpointAttribute : AuthorizeAttribute
{
    public const string PolicyName = "InstitutionExamAccess";
    public const string RolloutKey = "Multitenancy:ExamApi:Enabled";
    public InstitutionExamEndpointAttribute() => Policy = PolicyName;
    internal static bool Enabled(IConfiguration? configuration) => InstitutionSchoolEndpointAttribute.Enabled(configuration) && bool.TryParse(configuration?[RolloutKey], out var enabled) && enabled;
}
