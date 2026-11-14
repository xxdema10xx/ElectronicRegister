namespace ElectronicRegisterAPI.Domain.Models;

// Stored form of a fiscal code: HMAC for uniqueness/search and AES-GCM ciphertext
// (nonce || ciphertext || tag), each with the version of the key that produced it.
public sealed record ProtectedFiscalCode(
    string Hmac,
    string HmacKeyId,
    byte[] Cipher,
    string CipherKeyId
)
{
    // The default ToString would print every member: a logged instance must never reveal the HMAC or the ciphertext.
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Hmac=<redacted>, Cipher=<redacted>");
        return true;
    }
}
