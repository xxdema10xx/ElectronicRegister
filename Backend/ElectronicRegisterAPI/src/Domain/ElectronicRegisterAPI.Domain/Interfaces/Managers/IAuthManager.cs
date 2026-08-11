using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IAuthManager
    {
        Task<string?> LoginAsync(LoginDto dto);
        Task<string?> MicrosoftLoginAsync(MicrosoftLoginDto dto);
        Task RegisterAsync(RegisterDto dto);
        Task RegisterForAdminAsync(RegisterForAdminDto dto);
        Task<UserDto?> GetCurrentUserAsync(ClaimsContext caller);
    }
}
