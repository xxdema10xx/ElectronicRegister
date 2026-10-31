using System.Text;

namespace ElectronicRegisterAPI.Domain.Policies;

public static class PasswordRules
{
    public static void Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || Encoding.UTF8.GetByteCount(password) > 72 || !password.Any(c => "!@#$%^&*()_-+=<>?/[]{}".Contains(c)))
            throw new ArgumentException("Password non valida: almeno 8 caratteri, un carattere speciale e massimo 72 byte UTF-8.");
    }
}
