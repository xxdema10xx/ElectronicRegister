namespace ElectronicRegisterAPI.Domain.Models;

// Stored form of a fiscal code: HMAC for uniqueness/search and AES-GCM ciphertext
// (nonce || ciphertext || tag), each with the version of the key that produced it.
public sealed record ProtectedFiscalCode(
    string Hmac,
    string HmacKeyId,
    byte[] Cipher,
    string CipherKeyId
);
