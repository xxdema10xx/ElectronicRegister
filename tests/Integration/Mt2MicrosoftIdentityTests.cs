using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Infrastructure.Options;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mt2MicrosoftIdentityTests(IntegrationFixture fixture)
{
    [Theory]
    [InlineData("linked", true)]
    [InlineData("email-only", false)]
    [InlineData("wrong-object", false)]
    [InlineData("wrong-tenant", false)]
    [InlineData("foreign-issuer", false)]
    [InlineData("expired", false)]
    [InlineData("wrong-audience", false)]
    [InlineData("untrusted-signature", false)]
    [InlineData("missing-object", false)]
    [InlineData("deleted-user", false)]
    [InlineData("duplicate-binding", false)]
    public async Task MicrosoftRequiresValidatedStableExplicitBinding(string scenario, bool accepted)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var id = Guid.NewGuid();
        const string tid = "11111111-2222-3333-4444-555555555555";
        var oid = Guid.NewGuid();
        const string issuer = "https://login.microsoftonline.com/" + tid + "/v2.0";
        var email = "mt2-microsoft-" + id.ToString("N") + "@example.test";
        using (var setup = fixture.ScopeFactory.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            if (scenario != "deleted-user")
            {
                db.Users.Add(new() { Id = id, Email = email, PasswordHash = "", Role = "admin" });
                await db.SaveChangesAsync();
            }
        }
        try
        {
            using var rsa = RSA.Create(2048);
            using var foreign = RSA.Create(2048);
            var key = new RsaSecurityKey(rsa) { KeyId = "mt2-microsoft" };
            var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
            configuration.SigningKeys.Add(key);
            var services = Phase3.Services(fixture);
            services.RemoveAll<ConfigurationManager<OpenIdConnectConfiguration>>();
            services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>("https://metadata.invalid", new Retriever(configuration)));
            services.Configure<AzureAdOptions>(o => { o.TenantId = tid; o.Audience = "mt2-microsoft"; });
            services.Configure<JwtOptions>(o => { o.Key = "mt2-microsoft-test-signing-key-0123456789abcdef"; o.Issuer = "mt2"; o.Audience = "mt2-client"; });
            var binding = new MicrosoftIdentityBinding { TenantId = Guid.Parse(tid), ObjectId = oid, UserId = id };
            services.Configure<MicrosoftIdentityOptions>(o => o.Bindings = scenario == "email-only" ? Array.Empty<MicrosoftIdentityBinding>() :
                scenario == "duplicate-binding" ? new[] { binding, binding } : new[] { binding });
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            using var scope = provider.CreateScope();
            var claims = new System.Collections.Generic.List<Claim>
            {
                new("tid", scenario == "wrong-tenant" ? Guid.NewGuid().ToString() : tid),
                new("preferred_username", email), new("email", email)
            };
            if (scenario != "missing-object") claims.Add(new("oid", scenario == "wrong-object" ? Guid.NewGuid().ToString() : oid.ToString()));
            var jwt = new JwtSecurityToken(scenario == "foreign-issuer" ? "https://foreign.invalid/v2.0" : issuer,
                scenario == "wrong-audience" ? "foreign" : "mt2-microsoft", claims,
                expires: scenario == "expired" ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new(scenario == "untrusted-signature" ? new RsaSecurityKey(foreign) { KeyId = key.KeyId } : key, SecurityAlgorithms.RsaSha256));
            var result = await scope.ServiceProvider.GetRequiredService<IInstitutionAuthManager>()
                .MicrosoftLoginAsync(new MicrosoftLoginDto { AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt) });
            Assert.Equal(accepted, result is not null);
            if (result is not null)
            {
                var ticket = new JwtSecurityTokenHandler().ReadJwtToken(result.Ticket);
                Assert.Equal(id.ToString(), ticket.Subject);
                Assert.Equal("er-selection+jwt", ticket.Header.Typ);
                Assert.DoesNotContain(ticket.Claims, c => c.Type is "institution_id" or "membership_id" or "role" or "institution_role");
            }
            // Validated provider/object binding never provisions a membership.
            Assert.False(await scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().InstitutionMemberships.AnyAsync(m=>m.UserId==id));
            // Controlled metadata replaces only the external discovery dependency; real MVC/JWT/manager/MySQL.
            await using var http = await Phase2C_HttpHost.StartAsync(fixture.ConnectionString, registrations =>
            {
                registrations.RemoveAll<ConfigurationManager<OpenIdConnectConfiguration>>();
                registrations.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>("https://metadata.invalid", new Retriever(configuration)));
                registrations.Configure<AzureAdOptions>(o => { o.TenantId = tid; o.Audience = "mt2-microsoft"; });
                registrations.Configure<MicrosoftIdentityOptions>(o => o.Bindings = scenario == "email-only" ? Array.Empty<MicrosoftIdentityBinding>() :
                    scenario == "duplicate-binding" ? new[] { binding, binding } : new[] { binding });
            });
            var response = await http.Client.PostAsJsonAsync("/api/Auth/identity/microsoft-login",
                new MicrosoftLoginDto { AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt) });
            Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
            if (accepted)
            {
                var identity = await response.Content.ReadFromJsonAsync<IdentityTicketDto>();
                http.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", identity!.Ticket);
                var institutions = await http.Client.GetFromJsonAsync<InstitutionChoiceDto[]>("/api/Auth/institutions");
                Assert.Empty(institutions!);
            }
            using var verify = fixture.ScopeFactory.CreateScope();
            Assert.False(await verify.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().InstitutionMemberships.AnyAsync(m => m.UserId == id));
        }
        finally
        {
            using var cleanup = fixture.ScopeFactory.CreateScope();
            await cleanup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Users.Where(u => u.Id == id).ExecuteDeleteAsync();
        }
    }

    private sealed class Retriever(OpenIdConnectConfiguration configuration) : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel) => Task.FromResult(configuration);
    }
}
