using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Business.Services;

internal class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    private static bool HasSpecialChar(string password)
    {
        return password.Any(c => "!@#$%^&*()_-+=<>?/[]{}".Contains(c));
    }

    private bool IsValidName(string name, int minLength)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Length >= minLength;
    }

    public void EnsureEmailIsValid(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("L'email non può essere vuota.");
        if (!email.Contains("@") ||
            !email.StartsWith("allievo_") ||
            !email.EndsWith("@itsumbria.it"))
            throw new ArgumentException("L'email non è valida.");
    }

    public async Task EnsureEmailIsAvailableAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user != null)
            throw new InvalidOperationException("L'email è già in uso.");
    }

    public void EnsureValidPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("La password non può essere vuota.");
        if (password.Length < 8)
            throw new ArgumentException("La password deve contenere almeno 8 caratteri.");
        if (!HasSpecialChar(password))
            throw new ArgumentException("La password deve contenere almeno un carattere speciale.");
    }

    public void EnsurePasswordMatches(string password, string passwordHash)
    {
        if (!_passwordHasher.Verify(password, passwordHash))
            throw new ArgumentException("La password non corrisponde.");
    }

    public void EnsureValidRole(string role)
    {
        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out _))
            throw new ArgumentException("Il ruolo non è valido.");
    }

    public void EnsureValidName(string name, int minLength)
    {
        if (!IsValidName(name, minLength))
            throw new ArgumentException($"Il nome deve contenere almeno {minLength} caratteri.");
    }

}

