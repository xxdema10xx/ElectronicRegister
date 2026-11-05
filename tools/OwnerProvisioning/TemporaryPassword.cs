using System.Security.Cryptography;

namespace ElectronicRegister.OwnerProvisioning;

// System-generated temporary credential: 4 groups of 5 characters from an unambiguous
// alphabet joined by '-', which satisfies the API PasswordRules (>= 8 chars, one special,
// <= 72 UTF-8 bytes). About 116 bits of entropy. Returned once, never stored or logged.
public static class TemporaryPassword
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
    public const int WorkFactor = 11; // Same BCrypt work factor as the API hasher.

    public static string New() => string.Join('-', Enumerable.Range(0, 4).Select(_ => new string(RandomNumberGenerator.GetItems<char>(Alphabet, 5))));

    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
}
