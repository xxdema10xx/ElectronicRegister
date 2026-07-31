using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Claims;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IMicrosoftTokenValidator
{
    Task<ClaimsPrincipal> ValidateAsync(string accessToken);
}
