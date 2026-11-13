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
using MySqlConnector;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
            ["Identity:Legacy:Enabled"] = "false",
            ["Multitenancy:SchoolApi:Enabled"] = "true",
            ["Multitenancy:ExamApi:Enabled"] = "true"
        });
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddBusinessServices();
        builder.Services.AddApplicationManagers();
        builder.Services.AddApiServices();
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(InstitutionExamController).Assembly)
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

    public HttpClient As(FixtureActor? caller)
    {
        Client.DefaultRequestHeaders.Authorization = caller is null ? null : new AuthenticationHeaderValue("Bearer", Token(caller));
        return Client;
    }

    private string Token(FixtureActor caller)
    {
        // Fixture issuance uses the persisted explicit membership, never caller.Role
        // or global/profile claims. The real canonical JWT and live IAM handlers run.
        using var scope=_app.Services.CreateScope();
        using var connection=new MySqlConnection(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Database.GetConnectionString());
        connection.Open();
        using var command=new MySqlCommand("SELECT m.id,s.security_generation,r.role FROM institution_membership m JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id JOIN institution_membership_role r ON r.membership_id=m.id WHERE m.institution_id=@tenant AND m.user_id=@user ORDER BY r.role",connection);
        command.Parameters.AddWithValue("@tenant",SeedData.CoreInstitutionId);command.Parameters.AddWithValue("@user",caller.UserId);
        Guid membership=Guid.NewGuid();long generation=0;var roles=new List<InstitutionRole>();
        using(var reader=command.ExecuteReader())while(reader.Read()){membership=reader.GetGuid(0);generation=reader.GetInt64(1);roles.Add(Enum.Parse<InstitutionRole>(reader.GetString(2)));}
        return scope.ServiceProvider.GetRequiredService<IInstitutionTokens>().Access(new(caller.UserId,SeedData.CoreInstitutionId,membership,roles,generation));
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
