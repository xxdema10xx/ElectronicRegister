using System.Security.Cryptography;

namespace ElectronicRegisterAPI.Domain.Policies;

// System-generated temporary credential: 4 groups of 5 characters from an unambiguous
// alphabet joined by '-', compliant with PasswordRules, about 116 bits of entropy.
public static class TemporaryPasswords
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    public static string New()
    {
        var value = string.Join('-', Enumerable.Range(0, 4).Select(_ => new string(RandomNumberGenerator.GetItems<char>(Alphabet, 5))));
        PasswordRules.Validate(value);
        return value;
    }
}
