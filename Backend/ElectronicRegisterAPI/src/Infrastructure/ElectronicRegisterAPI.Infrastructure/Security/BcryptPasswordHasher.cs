using ElectronicRegisterAPI.Domain.Interfaces.Security;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal class BcryptPasswordHasher : IPasswordHasher
{
    // One work factor for real and absent credentials, so login cost does not reveal an email.
    internal const int WorkFactor = 11;
    private static readonly Lazy<string> Absent = new(() => BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), WorkFactor));
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    public bool VerifyAbsent(string password)
    {
        _ = Verify(password, Absent.Value);
        return false;
    }
    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(hash))
            return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception ex)when (ex is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            return false;
        }
    }
}
