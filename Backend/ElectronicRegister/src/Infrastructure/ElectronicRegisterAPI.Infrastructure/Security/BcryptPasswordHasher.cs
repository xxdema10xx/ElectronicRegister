using ElectronicRegisterAPI.Domain.Interfaces.Security;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}