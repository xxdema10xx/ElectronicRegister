using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Controllers;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class AuditBootstrapTests(IntegrationFixture fixture)
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task RealProgram_StartsAuthenticatesDocumentsAndStops(string environment)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var id = Guid.NewGuid();
        var email = "bootstrap-" + id.ToString("N") + "@example.test";
        const string password = "AuditBootstrapPassword!";
        using (var setup = fixture.ScopeFactory.CreateScope())
        {
            var ctx = setup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            ctx.Users.Add(new() { Id = id, Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = "admin" });
            await ctx.SaveChangesAsync();
        }
        var observer = new HostObserver();
        using var subscription = DiagnosticListener.AllListeners.Subscribe(observer);
        var args = new[]
        {
            "--Deployment:Mode=LocalTest", "--environment=" + environment, "--urls=http://127.0.0.1:0",
            // In-process entry point invocation must retain the production assembly identity.
            "--applicationName=" + typeof(InstitutionExamController).Assembly.GetName().Name,
            "--ConnectionStrings:DefaultConnection=" + fixture.ConnectionString,
            "--Jwt:Key=audit-bootstrap-test-key-only-0123456789abcdef",
            "--Jwt:Issuer=audit-bootstrap", "--Jwt:Audience=audit-client", "--Jwt:ExpiresInMinutes=10",
            "--AzureAd:TenantId=11111111-2222-3333-4444-555555555555", "--AzureAd:Audience=audit-microsoft",
            "--Identity:Legacy:Enabled=true", "--Identity:Legacy:InstitutionId=71000000-0000-0000-0000-000000000001",
            "--Identity:Legacy:AllowUnbackfilledAccounts=true",
            "--Identity:Legacy:UserIds:0=" + id,
            "--Logging:LogLevel:Default=Information",
            // Match the other in-process Program fixtures: Windows EventLog is
            // not writable by the test account and must not mask HTTP errors.
            "--Logging:EventLog:LogLevel:Default=None"
        };
        if (Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_PORT") is { } isolatedPort)
            args = args.Append("--Deployment:LocalTestDatabasePort=" + isolatedPort).ToArray();
        // Execute the actual assembly entry point. No replacement host, registration, worker or middleware.
        var run = Task.Run(async () =>
        {
            var result = typeof(InstitutionExamController).Assembly.EntryPoint!.Invoke(null, new object[] { args });
            if (result is Task task) await task;
        });
        IHost? host = null;
        var logs = new AuditLogs();
        try
        {
            var completed = await Task.WhenAny(observer.Built.Task, run).WaitAsync(TimeSpan.FromSeconds(30));
            if (completed == run) await run; // Propagate bootstrap failure instead of timing out.
            host = await observer.Built.Task.WaitAsync(TimeSpan.FromSeconds(30));
            host.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult(true));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Contains(host.Services.GetServices<IHostedService>(), s => s.GetType().Name == "ExpiredExamSubmissionWorker");
            Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<FixtureExamClient>());
            var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
            var address = Assert.Single(addresses);
            Assert.StartsWith("http://127.0.0.1:", address);
            Assert.DoesNotContain(":5000", address);
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(15) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/account")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/Auth/me")).StatusCode);
            var login = await client.PostAsJsonAsync("/api/Auth/identity/login", new LoginDto { Email = email, Password = password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var token = (await login.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var me = await client.GetFromJsonAsync<IdentityAccountDto>("/api/identity/account");
            Assert.Equal(id, me!.UserId);
            Assert.Equal(email, me.Email);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/Exam")).StatusCode);
            foreach (var fault in new[] { "issuer", "audience", "expired", "key", "claims" })
            {
                var invalid = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                    issuer: fault == "issuer" ? "foreign" : "audit-bootstrap",
                    audience: fault == "audience" ? "foreign" : "audit-client",
                    claims: new[]
                    {
                        new System.Security.Claims.Claim("sub", fault == "claims" ? "not-a-guid" : id.ToString()),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")
                    },
                    expires: fault == "expired" ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(10),
                    signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                        new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(
                            fault == "key" ? "foreign-bootstrap-test-key-only-0123456789abcdef" : "audit-bootstrap-test-key-only-0123456789abcdef")),
                        Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                    new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(invalid));
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await client.GetAsync("/api/identity/account")).StatusCode);
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await CheckEndpointRoleGatesAsync(host, client, id);
            var error = await client.PostAsJsonAsync("/api/Auth/select-institution", new SelectInstitutionDto { InstitutionId=Guid.NewGuid(), MembershipId=Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.Forbidden,error.StatusCode);
            var problem = await error.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(403, problem.GetProperty("status").GetInt32());
            Assert.False(problem.TryGetProperty("stackTrace", out _));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/Auth/select-institution", new { institutionId="not-a-guid", membershipId=Guid.NewGuid() })).StatusCode);
            var openApi = await client.GetAsync("/openapi/v1.json");
            Assert.Equal(environment == "Development" ? HttpStatusCode.OK : HttpStatusCode.NotFound, openApi.StatusCode);
            Assert.Equal(environment == "Development" ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                (await client.GetAsync("/scalar/v1")).StatusCode);
            if (environment == "Development")
            {
                var document = await openApi.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                Assert.False(document.GetProperty("paths").TryGetProperty("/api/Exam/{id}/close", out _));
                Assert.True(document.GetProperty("components").GetProperty("schemas").TryGetProperty("IdentityAccountDto", out _));
                Assert.False(document.GetProperty("paths").TryGetProperty("/api/Auth/login", out _));
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^8] + "tampered");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/account")).StatusCode);
            await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await run.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
            Assert.DoesNotContain(logs.Errors, entry => entry.Contains("ExpiredExamSubmissionWorker") || entry.Contains("Application startup exception"));
        }
        finally
        {
            if (host is not null && !run.IsCompleted) { await host.StopAsync(); await run.WaitAsync(TimeSpan.FromSeconds(30)); }
            observer.Dispose();
            using var cleanup = fixture.ScopeFactory.CreateScope();
            await cleanup.ServiceProvider.GetRequiredService<ElectronicRegisterContext>().Users.Where(u => u.Id == id).ExecuteDeleteAsync();
        }
    }

    private static async Task CheckEndpointRoleGatesAsync(IHost host, HttpClient client, Guid id)
    {
        var endpoints = host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is not null).ToList();
        var schoolEndpoints = endpoints.Where(e => e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionSchoolEndpointAttribute>() is not null).ToList();
        Assert.Equal(76, schoolEndpoints.Count);
        var examEndpoints = endpoints.Where(e => e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionExamEndpointAttribute>() is not null).ToList();
        Assert.Equal(38, examEndpoints.Count);
        var courseEndpoints = endpoints.Where(e => e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionCourseEndpointAttribute>() is not null).ToList();
        Assert.Equal(23, courseEndpoints.Count);
        Assert.All(courseEndpoints, e => Assert.Contains(e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            a => a.Policy == ElectronicRegisterAPI.Api.Security.InstitutionCourseEndpointAttribute.PolicyName));
        var membershipEndpoints = endpoints.Where(e => e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionMembershipAdminEndpointAttribute>() is not null).ToList();
        MembershipHttpSurface.AssertRoutes(endpoints);
        Assert.Equal(MembershipHttpSurface.AdminCount, membershipEndpoints.Count);
        Assert.All(membershipEndpoints, e => Assert.Contains(e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            a => a.Policy == ElectronicRegisterAPI.Api.Security.InstitutionMembershipAdminEndpointAttribute.PolicyName));
        Assert.Equal(MembershipHttpSurface.LegacyCount + MembershipHttpSurface.IdentityCount + schoolEndpoints.Count + examEndpoints.Count + courseEndpoints.Count + membershipEndpoints.Count, endpoints.Count);
        Assert.Equal(MembershipHttpSurface.IdentityCount,endpoints.Count(e=>e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionIdentityEndpointAttribute>() is not null));
        Assert.All(schoolEndpoints, e => Assert.Contains(e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            a => a.Policy == ElectronicRegisterAPI.Api.Security.InstitutionSchoolEndpointAttribute.PolicyName));
        var legacyEndpoints = endpoints.Where(e => e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionIdentityEndpointAttribute>() is null &&
            e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionSchoolEndpointAttribute>() is null &&
            e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionExamEndpointAttribute>() is null &&
            e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionCourseEndpointAttribute>() is null &&
            e.Metadata.GetMetadata<ElectronicRegisterAPI.Api.Security.InstitutionMembershipAdminEndpointAttribute>() is null).ToList();
        Assert.Empty(legacyEndpoints);
        foreach (var endpoint in legacyEndpoints)
        {
            var authorization = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
            if (authorization.Count == 0) continue; // The two login routes are intentionally anonymous.
            var allowed = authorization.SelectMany(a => (a.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)).ToHashSet();
            var deniedRole = new[] { "student", "teacher", "admin", "unrecognized" }.First(r => !allowed.Contains(r));
            var deniedToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("audit-bootstrap", "audit-client",
                new[] { new System.Security.Claims.Claim("sub", id.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, deniedRole) },
                expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("audit-bootstrap-test-key-only-0123456789abcdef")),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
            var encoded = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(deniedToken);
            var path = System.Text.RegularExpressions.Regex.Replace(endpoint.RoutePattern.RawText!, @"\{([^}:?]+)[^}]*\}", match =>
                match.Groups[1].Value == "date" ? "2026-10-18" : match.Groups[1].Value is "lastName" or "name" or "subject" ? "Audit" : Guid.NewGuid().ToString());
            var methods = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods;
            foreach (var method in methods)
            foreach (var authenticated in new[] { false, true })
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), "/" + path.TrimStart('/'));
                // Override the client's valid default credential on each request.
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticated ? encoded : "");
                if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });
                var response = await client.SendAsync(request);
                Assert.True(response.StatusCode == (authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized),
                    $"Unexpected role gate: {method} {path}, role={deniedRole}, authenticated={authenticated}, status={response.StatusCode}");
            }
        }
    }

    private sealed class HostObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        public TaskCompletionSource<IHost> Built { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<IDisposable> _subscriptions = new();
        public void OnNext(DiagnosticListener listener) { if (listener.Name == "Microsoft.Extensions.Hosting") _subscriptions.Add(listener.Subscribe(this)); }
        public void OnNext(KeyValuePair<string, object?> item)
        {
            if(item.Key=="HostBuilding" && item.Value is IHostBuilder builder)builder.ConfigureServices((_,services)=>TestHostIsolation.Configure(services));
            if (item.Key == "HostBuilt" && item.Value is IHost host) Built.TrySetResult(host);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => Built.TrySetException(error);
        public void Dispose() { foreach (var item in _subscriptions) item.Dispose(); }
    }

    private sealed class AuditLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Errors { get; } = new();
        public ILogger CreateLogger(string categoryName) => new AuditLogger(categoryName, Errors);
        public void Dispose() { }
        private sealed class AuditLogger(string category, ConcurrentQueue<string> errors) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => level >= LogLevel.Error;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error, Func<TState, Exception?, string> formatter)
            { if (level >= LogLevel.Error) errors.Enqueue(category + ": " + formatter(state, error) + "; " + error); }
        }
    }
}
