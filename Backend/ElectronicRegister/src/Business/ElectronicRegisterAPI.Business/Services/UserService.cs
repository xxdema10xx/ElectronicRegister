using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;

namespace ElectronicRegisterAPI.Business.Services;

internal class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    private readonly int _minFirstNameLength = 3;
    private readonly int _minLastNameLength = 2;

    public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    private static bool HasSpecialChar(string password)
    {
        return password.Any(c => "!@#$%^&*()_-+=<>?/[]{}".Contains(c));
    }

    private bool IsValidName(string firstName, string lastName)
    {
        return !string.IsNullOrWhiteSpace(firstName) && firstName.Length >= _minFirstNameLength &&
               !string.IsNullOrWhiteSpace(lastName) && lastName.Length >= _minLastNameLength;
    }

    public void EnsureEmailIsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("L'email è obbligatoria.");
    }

    public async Task EnsureEmailIsAvailableAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user != null)
            throw new InvalidOperationException("L'email è già in uso.");
    }

    public void EnsureValidPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            throw new ArgumentException("La password deve contenere almeno 8 caratteri.");
        if (!HasSpecialChar(password))
            throw new ArgumentException("La password deve contenere almeno un carattere speciale.");
    }

    public void EnsurePasswordMatches(string? password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || !_passwordHasher.Verify(password, passwordHash))
            throw new ArgumentException("La password non corrisponde.");
    }

    public void EnsureCallerCanChangePassword(ClaimsContext caller, Guid targetUserId)
    {
        if (caller.Role != UserRole.Admin && caller.UserId != targetUserId)
            throw new UnauthorizedAccessException("Non puoi modificare la password di un altro utente.");
    }

    public void EnsureValidRole(string role)
    {
        if (Enum.TryParse<UserRole>(role, ignoreCase: true, out _))
            throw new ArgumentException("Il ruolo non è valido.");
    }

    public void EnsureValidName(string firstName, string lastName)
    {
        if (!IsValidName(firstName, lastName))
            throw new ArgumentException("Il nome e il cognome sono obbligatori.");
    }

    public void EnsureSelfRegistrationEmailFormat(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.StartsWith("allievo_") || !email.EndsWith("@itsumbria.it"))
            throw new ArgumentException("Formato email non valido!");
    }

    public UserRole ParseRole(string role) => role.ToLowerInvariant() switch
    {
        "admin" => UserRole.Admin,
        "teacher" => UserRole.Teacher,
        "student" => UserRole.Student,
        _ => throw new ArgumentException($"Ruolo non valido: {role}")
    };

}

