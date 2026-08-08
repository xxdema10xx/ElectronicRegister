using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
public interface IUserService
{
    void EnsureEmailIsValid(string? email);
    Task EnsureEmailIsAvailableAsync(string email);
    void EnsureValidPassword(string? password);
    void EnsurePasswordMatches(string? password, string passwordHash);
    void EnsureValidRole(string role);
    void EnsureValidName(string firstName, string lastName);
    void EnsureCallerCanChangePassword(ClaimsContext caller, Guid targetUserId);
    void EnsureSelfRegistrationEmailFormat(string email);
    UserRole ParseRole(string role);
}
