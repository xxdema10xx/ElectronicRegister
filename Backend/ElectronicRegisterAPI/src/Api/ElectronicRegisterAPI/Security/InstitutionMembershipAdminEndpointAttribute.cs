using Microsoft.AspNetCore.Authorization;
namespace ElectronicRegisterAPI.Api.Security;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class InstitutionMembershipAdminEndpointAttribute : AuthorizeAttribute
{
    public const string PolicyName = "InstitutionMembershipAdminAccess";
    public const string RolloutKey = "Multitenancy:MembershipAdminApi:Enabled";
    public InstitutionMembershipAdminEndpointAttribute() => Policy = PolicyName;
    internal static bool Enabled(IConfiguration? configuration) =>
        bool.TryParse(configuration?[RolloutKey], out var enabled) && enabled;
}
