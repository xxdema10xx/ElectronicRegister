namespace ElectronicRegisterAPI.Api.Security;

// Supplemental capability marker, never an alternative authorization scheme/policy.
[AttributeUsage(AttributeTargets.Method,Inherited=false)]
public sealed class InstitutionMembershipWriteEndpointAttribute : Attribute
{
    public const string RolloutKey="Multitenancy:MembershipWriteApi:Enabled";
    internal static bool Enabled(IConfiguration? configuration) =>
        bool.TryParse(configuration?[RolloutKey],out var value) && value;
}
