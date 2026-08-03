using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers
{
    public interface IAuthManager
    {
        Task MicrosoftLoginAsync(MicrosoftLoginDto dto);
        Task LoginAsync(LoginDto dto);
        Task<ClaimsPrincipal> ValidateMicrosoftTokenAsync(string token);
        Task RegisterAsync (RegisterDto dto, ClaimsContext caller);
        Task RegisterForAdminAsync(RegisterForAdminDto dto, ClaimsContext caller);
        Task<UserDto> GetCurrentUserAsync ();
    }
}
