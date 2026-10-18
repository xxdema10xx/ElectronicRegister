using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Controllers;
using ElectronicRegisterAPI.Api.DependencyInjection;
using ElectronicRegisterAPI.Application.DependencyInjection;
using ElectronicRegisterAPI.Business.DependencyInjection;
using ElectronicRegisterAPI.Domain.Constants;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ElectronicRegisterAPI.Tests.Integration;

// Real Kestrel, JWT authentication, MVC, exception handler, managers and MySQL repositories.
// No extra TestHost dependency and no database replacement. Only the background worker is omitted.
internal sealed class Phase2C_HttpHost : IAsyncDisposable
{
    private const string Issuer = "phase2c-tests";
    private const string Key = "phase2c-only-test-signing-key-0123456789abcdef";
    private readonly WebApplication _app;
    public HttpClient Client { get; }

    private Phase2C_HttpHost(WebApplication app, HttpClient client) => (_app, Client) = (app, client);

    public static async Task<Phase2C_HttpHost> StartAsync(string connectionString, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Jwt:Key"] = Key, ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Issuer,
            ["Identity:Legacy:Enabled"] = "true",
            ["Identity:Legacy:AllowUnbackfilledAccounts"] = "true",
            ["Identity:Legacy:InstitutionId"] = "71000000-0000-0000-0000-000000000001"
        });
        // Explicit test-only migration manifest, including scenarios created after host startup.
        var legacyIds = Enum.GetValues<IntegrationScenario>().Select(s => ScenarioIds.For(s).StudentId)
            .Concat(new[] { SeedData.AdminUserId, SeedData.TeacherUserId, SeedData.Id(0, 13) }).Distinct().ToArray();
        builder.Configuration.AddInMemoryCollection(legacyIds.Select((id, index) =>
            new KeyValuePair<string, string?>($"Identity:Legacy:UserIds:{index}", id.ToString())));
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddBusinessServices();
        builder.Services.AddApplicationManagers();
        builder.Services.AddApiServices();
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(ExamController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = Issuer,
                ValidateAudience = true, ValidAudience = Issuer,
                ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
            };
        });
        builder.Services.AddAuthorization();
        ElectronicRegisterAPI.Api.Security.InstitutionAuthentication.AddInstitutionAuthentication(builder.Services, builder.Configuration);
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        // Deterministic probes exercise the real handler without manufacturing database corruption.
        app.MapGet("/test/internal-error", () => Task.FromException(new InvalidOperationException("sensitive internal database information")));
        app.MapGet("/test/domain-error", () => Task.FromException(SubmissionErrors.Conflict("sensitive domain information")));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new Phase2C_HttpHost(app, new HttpClient { BaseAddress = new Uri(address) });
    }

    public HttpClient As(ClaimsContext? caller)
    {
        Client.DefaultRequestHeaders.Authorization = caller is null ? null : new AuthenticationHeaderValue("Bearer", Token(caller));
        return Client;
    }

    private static string Token(ClaimsContext caller)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, caller.UserId.ToString()),
            new(ClaimTypes.Role, caller.Role.ToString().ToLowerInvariant())
        };
        if (caller.StudentId.HasValue) claims.Add(new(CustomClaimTypes.StudentId, caller.StudentId.Value.ToString()));
        if (caller.TeacherId.HasValue) claims.Add(new(CustomClaimTypes.TeacherId, caller.TeacherId.Value.ToString()));
        var token = new JwtSecurityToken(Issuer, Issuer, claims, expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
