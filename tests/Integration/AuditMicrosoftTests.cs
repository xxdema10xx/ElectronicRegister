using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class AuditMicrosoftTests(IntegrationFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MicrosoftValidator_RequiresConfiguredTenantIssuer(bool correctIssuer)
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "audit-key" };
        const string tenant = "11111111-2222-3333-4444-555555555555";
        const string issuer = "https://login.microsoftonline.com/" + tenant + "/v2.0";
        const string audience = "audit-api";
        var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
        configuration.SigningKeys.Add(key);
        var services = Phase3.Services(fixture);
        services.RemoveAll<ConfigurationManager<OpenIdConnectConfiguration>>();
        services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>("https://metadata.invalid", new AuditRetriever(configuration)));
        services.Configure<AzureAdOptions>(o => { o.TenantId = tenant; o.Audience = audience; });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IMicrosoftTokenValidator>();
        var token = new JwtSecurityToken(correctIssuer ? issuer : "https://login.microsoftonline.com/foreign-tenant/v2.0",
            audience, new[] { new Claim("preferred_username", "admin@example.test"), new Claim("tid", correctIssuer ? tenant : "foreign-tenant") },
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        if (correctIssuer) Assert.NotNull(await validator.ValidateAsync(encoded));
        else await Assert.ThrowsAsync<SecurityTokenInvalidIssuerException>(() => validator.ValidateAsync(encoded));
    }

    private sealed class AuditRetriever(OpenIdConnectConfiguration configuration) : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
            => Task.FromResult(configuration);
    }
}
