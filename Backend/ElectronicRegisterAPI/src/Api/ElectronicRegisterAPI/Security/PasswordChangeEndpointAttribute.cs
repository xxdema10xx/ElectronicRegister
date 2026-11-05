namespace ElectronicRegisterAPI.Api.Security;

// The only endpoint a selection ticket may reach while its account holds a temporary
// password. Enforced at token validation, so no other endpoint can opt out.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PasswordChangeEndpointAttribute : Attribute
{
}
