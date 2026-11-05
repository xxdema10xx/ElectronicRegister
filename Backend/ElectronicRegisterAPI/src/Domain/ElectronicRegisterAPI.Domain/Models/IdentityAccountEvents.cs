namespace ElectronicRegisterAPI.Domain.Models;

// Append-only identity_account_event actions. The latest credential event decides
// whether the account must change its password: a temporary credential (account
// provisioning or owner reset) stays mandatory until a later password change.
public static class IdentityAccountEvents
{
    public const string Provisioned = "account.provisioned";
    public const string TemporaryPassword = "password.temporary";
    public const string PasswordChanged = "password.changed";
}
