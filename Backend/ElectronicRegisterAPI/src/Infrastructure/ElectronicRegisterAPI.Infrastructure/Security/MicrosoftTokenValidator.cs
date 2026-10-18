using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal class MicrosoftTokenValidator : IMicrosoftTokenValidator
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly AzureAdOptions _options;

    public MicrosoftTokenValidator(
        ConfigurationManager<OpenIdConnectConfiguration> configManager,
        IOptions<AzureAdOptions> options)
    {
        _configManager = configManager;
        _options = options.Value;
    }

    public async Task<ClaimsPrincipal> ValidateAsync(string accessToken)
    {
        var config = await _configManager.GetConfigurationAsync();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://login.microsoftonline.com/{_options.TenantId}/v2.0",
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(accessToken, validationParameters, out _);
    }
}
