using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface IAuthService
    {
        Task<string> AuthenticateAsync(string email, string password);
        Task<string> AuthenticateMicrosoftAsync(string accessToken);
    }
}
