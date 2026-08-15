using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

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
    void EnsureUserCanBeDeletedAsync(User user);
    UserRole ParseRole(string role);
}
