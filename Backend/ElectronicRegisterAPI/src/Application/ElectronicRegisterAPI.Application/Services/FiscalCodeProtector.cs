using System.Security.Cryptography;
using System.Text;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Services;

// HMAC-SHA256 (uniqueness/search) and AES-256-GCM (recoverable value) with two distinct
// owner keys validated at startup. Each encryption uses a fresh 96-bit nonce. The associated
// data binds a stored ciphertext to its key version and HMAC, so it cannot be moved to
// another row; import staging tokens use a separate associated-data domain.
internal sealed class FiscalCodeProtector(PlatformIdentitySettings settings) : IFiscalCodeProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string TokenPrefix = "cf1:";

    public string Hmac(string normalizedFiscalCode)
    {
        settings.RequireEnabled();
        return Convert.ToHexString(HMACSHA256.HashData(settings.HmacKeyBytes, Encoding.UTF8.GetBytes(normalizedFiscalCode)));
    }

    public ProtectedFiscalCode Protect(string normalizedFiscalCode)
    {
        var hmac = Hmac(normalizedFiscalCode);
        return new(hmac, settings.HmacKeyId, Encrypt(Encoding.UTF8.GetBytes(normalizedFiscalCode), StoredData(settings.EncryptionKeyId, hmac)), settings.EncryptionKeyId);
    }

    public string Unprotect(ProtectedFiscalCode value)
    {
        settings.RequireEnabled();
        if (!string.Equals(value.CipherKeyId, settings.EncryptionKeyId, StringComparison.Ordinal))
            throw new CryptographicException("Versione della chiave di cifratura non disponibile.");
        var plain = Encoding.UTF8.GetString(Decrypt(value.Cipher, StoredData(value.CipherKeyId, value.Hmac)));
        if (string.Equals(value.HmacKeyId, settings.HmacKeyId, StringComparison.Ordinal) && !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hmac(plain)), Encoding.ASCII.GetBytes(value.Hmac)))
            throw new CryptographicException("Codice fiscale protetto incoerente.");
        return plain;
    }

    public string Seal(string value)
    {
        settings.RequireEnabled();
        if (value is null || value.Length > 64)
            throw new ArgumentException("Valore da proteggere non valido.");
        return TokenPrefix + settings.EncryptionKeyId + ":" + Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(value), StagingData(settings.EncryptionKeyId)));
    }

    public string Unseal(string token)
    {
        settings.RequireEnabled();
        var parts = IsSealed(token) ? token[TokenPrefix.Length..].Split(':', 2) : [];
        if (parts.Length != 2 || !string.Equals(parts[0], settings.EncryptionKeyId, StringComparison.Ordinal))
            throw new CryptographicException("Valore protetto non valido.");
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException error)
        {
            throw new CryptographicException("Valore protetto non valido.", error);
        }
        return Encoding.UTF8.GetString(Decrypt(payload, StagingData(parts[0])));
    }

    public bool IsSealed(string? value) => value?.StartsWith(TokenPrefix, StringComparison.Ordinal) == true;

    private static byte[] StoredData(string keyId, string hmac) => Encoding.ASCII.GetBytes("er-cf-v1|" + keyId + "|" + hmac);
    private static byte[] StagingData(string keyId) => Encoding.ASCII.GetBytes("er-import-cf-v1|" + keyId);

    private byte[] Encrypt(byte[] plaintext, byte[] associatedData)
    {
        var output = new byte[NonceSize + plaintext.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(settings.EncryptionKeyBytes, TagSize);
        aes.Encrypt(nonce, plaintext, output.AsSpan(NonceSize, plaintext.Length), output.AsSpan(NonceSize + plaintext.Length, TagSize), associatedData);
        return output;
    }

    private byte[] Decrypt(byte[] payload, byte[] associatedData)
    {
        if (payload is null || payload.Length < NonceSize + TagSize + 1)
            throw new CryptographicException("Valore protetto non valido.");
        var length = payload.Length - NonceSize - TagSize;
        var plaintext = new byte[length];
        using var aes = new AesGcm(settings.EncryptionKeyBytes, TagSize);
        aes.Decrypt(payload.AsSpan(0, NonceSize), payload.AsSpan(NonceSize, length), payload.AsSpan(NonceSize + length, TagSize), plaintext, associatedData);
        return plaintext;
    }
}
