using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

// Fiscal code protection with owner keys: HMAC-SHA256 for uniqueness and lookup, AES-256-GCM
// for the recoverable value. Seal/Unseal protect transient import staging cells.
// Plaintext is never logged, audited, hashed into intents or returned by the API.
public interface IFiscalCodeProtector
{
    string Hmac(string normalizedFiscalCode);
    ProtectedFiscalCode Protect(string normalizedFiscalCode);
    string Unprotect(ProtectedFiscalCode value);
    string Seal(string value);
    string Unseal(string token);
    bool IsSealed(string? value);
}
