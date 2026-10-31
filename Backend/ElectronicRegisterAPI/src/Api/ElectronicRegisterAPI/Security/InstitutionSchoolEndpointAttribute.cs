using Microsoft.AspNetCore.Authorization;

namespace ElectronicRegisterAPI.Api.Security;

/// <summary>Explicit opt-in for the three migrated school API families.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class InstitutionSchoolEndpointAttribute : AuthorizeAttribute
{
    public const string PolicyName = "InstitutionSchoolAccess";
    public const string RolloutKey = "Multitenancy:SchoolApi:Enabled";
    public InstitutionSchoolEndpointAttribute() => Policy = PolicyName;
    internal static bool Enabled(IConfiguration? configuration) => bool.TryParse(configuration?[RolloutKey], out var enabled) && enabled;
}
