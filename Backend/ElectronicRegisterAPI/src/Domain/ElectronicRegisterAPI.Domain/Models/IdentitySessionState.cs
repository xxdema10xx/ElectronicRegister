namespace ElectronicRegisterAPI.Domain.Models;

// One snapshot of the identity row: the revocation generation and whether the
// current credential is a temporary one that must be replaced before any other use.
public sealed record IdentitySessionState(long Generation, bool PasswordChangeRequired);
