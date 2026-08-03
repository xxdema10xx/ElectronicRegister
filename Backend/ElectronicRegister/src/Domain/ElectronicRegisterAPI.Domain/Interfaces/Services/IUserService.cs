using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface IUserService
    {
        void EnsureEmailIsValid(string email);
        //void EnsureSelfRegistrationEmailFormat(string email); // ex IAuthService.ValidateRegistrationEmail
        Task EnsureEmailIsAvailableAsync(string email);
        void EnsureValidPassword(string password);
        void EnsurePasswordMatches(string password, string passwordHash);
        void EnsureValidRole(string role);
        void EnsureValidName(string name, int minLength);
    }
}
