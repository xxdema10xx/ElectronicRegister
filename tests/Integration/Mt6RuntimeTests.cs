using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Api.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection("Mt5aIdentityOnly")]
public sealed class Mt6RuntimeTests
{
    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static Dictionary<string, string?> Settings() => new()
    {
        ["Deployment:Mode"] = "LocalTest", ["urls"] = "http://127.0.0.1:0",
        ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Port=13306;Database=electronic_register_test",
        ["Jwt:Key"] = Mt3cHosting.Key, ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test"
    };

    [Theory]
    [InlineData("urls", "http://0.0.0.0:5000")]
    [InlineData("urls", "http://example.test:5000")]
    [InlineData("ConnectionStrings:DefaultConnection", "Server=127.0.0.1;Port=3306;Database=electronic_register_test")]
    [InlineData("Deployment:Mode", "")]
    [InlineData("Deployment:Mode", "Production")]
    [InlineData("Jwt:Key", "CHANGE-ME")]
    [InlineData("Transport:AllowedOrigins:0", "https://*.example.test")]
    [InlineData("Transport:AllowedOrigins:0", "https://example.test/path")]
    [InlineData("Transport:KnownProxies:0", "127.0.0.1")]
    [InlineData("Transport:SensitivePermitLimit", "0")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    public void UnsafeConfigurationRefusedBeforeAnyDatabaseConnection(string key, string value)
    {
        var settings = Settings(); settings[key] = value;
        Assert.Throws<InvalidOperationException>(() => RuntimePolicy.Read(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new EnvironmentStub()));
    }

    [Fact]
    public void ExplicitLoopbackLocalTestKeepsCorsClosedByDefault()
    {
        var policy = RuntimePolicy.Read(new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build(), new EnvironmentStub());
        Assert.True(policy.Local); Assert.Empty(policy.Origins); Assert.Empty(policy.Proxies);
    }

    [Fact]
    public void StrictStagingRequiresVerifiedTlsNonRootIdentityAndClosedRollout()
    {
        var values = Settings();
        values["Deployment:Mode"] = "Staging";
        values["Jwt:Key"] = new string('x', 64);
        values["AllowedHosts"] = "staging.example.test";
        values["Transport:HttpsPort"] = "443";
        values["ConnectionStrings:DefaultConnection"] = "Server=db.example.test;User ID=application;Password=synthetic-policy-value;SslMode=VerifyFull;SslCa=owner-ca.pem;Database=staging";
        values["Readiness:ExpectedVersion"] = "OWNER-CERTIFIED";
        values["Readiness:ExpectedHost"] = "expected-host";
        values["Readiness:ExpectedDataDir"] = "/expected-data";
        Assert.False(RuntimePolicy.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new EnvironmentStub { EnvironmentName = "Staging" }).Local);
        foreach (var pair in new[] { ("AllowedHosts", "*"), ("Identity:Legacy:Enabled", "true"), ("Multitenancy:Onboarding:Enabled", "true") })
        {
            var bad = new Dictionary<string, string?>(values) { [pair.Item1] = pair.Item2 };
            Assert.Throws<InvalidOperationException>(() => RuntimePolicy.Read(new ConfigurationBuilder().AddInMemoryCollection(bad).Build(), new EnvironmentStub { EnvironmentName = "Staging" }));
        }
    }

    [Fact]
    public async Task RealProgramRefusesIncompleteProductionConfigurationAtStartup()
    {
        var invocation = await Assert.ThrowsAsync<System.Reflection.TargetInvocationException>(async () =>
        {
            await using var host = await Mt3cHosting.ProgramHost.StartAsync(
                Settings()["ConnectionStrings:DefaultConnection"]!, Guid.Empty, [], enabled: false,
                extraArguments: ["--Deployment:Mode=Production", "--Jwt:Key=deliberately-incomplete"]);
        });
        var error = Assert.IsType<InvalidOperationException>(invocation.InnerException);
        Assert.Equal("Runtime security configuration is incomplete or unsafe.", error.Message);
        Assert.Contains("RuntimePolicy.Read", error.StackTrace);
    }

    [Fact]
    public async Task RealRequestsEmitBoundedMetricsWithoutTenantUserOrRequestLabels()
    {
        var count = 0L;
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) => { if (instrument.Meter.Name == OperationalPipeline.MeterName) l.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags) Assert.Equal("status", tag.Key);
            System.Threading.Interlocked.Add(ref count, value);
        });
        listener.Start();
        await using var host = await Start();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/live?not-logged=synthetic-secret")).StatusCode);
        Assert.True(count > 0);
    }

    private static Task<Mt3cHosting.ProgramHost> Start(params string[] args) => Mt3cHosting.ProgramHost.StartAsync(
        IntegrationFixture.ValidateLocalConnectionString(System.Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION") ?? throw new InvalidOperationException()),
        Guid.Empty, [], enabled: false, unbackfilled: false, extraArguments: args);

    [Fact]
    public async Task RealProgramLivenessReadinessHeadersAndShutdown()
    {
        await using var host = await Start();
        var endpoints = host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is null).ToArray();
        Assert.Equal(new[] { "/health/live", "/health/ready" }, endpoints.Select(e => e.RoutePattern.RawText).OrderBy(x => x));
        Assert.All(endpoints, e =>
        {
            Assert.Equal(new[] { "GET" }, e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods);
            Assert.NotNull(e.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>());
        });
        using var live = await host.Client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        foreach (var name in new[] { "X-Correlation-ID", "X-Content-Type-Options", "X-Frame-Options", "Referrer-Policy", "Content-Security-Policy" }) Assert.True(live.Headers.Contains(name), name);
        Assert.False(live.Headers.Contains("Server"));
        using var ready = await host.Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var body = await ready.Content.ReadAsStringAsync();
        Assert.DoesNotContain("13306", body); Assert.DoesNotContain("MAMP", body); Assert.DoesNotContain("5.7.24", body);
        Assert.Contains("no-store", ready.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task ReadinessWrongExpectedIdentityReturns503WithoutDetails()
    {
        await using var host = await Start("--Readiness:ExpectedHost=wrong-host");
        using var response = await host.Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task CorsRejectsUntrustedOriginsAndForwardingHeadersCannotSpoofHttps()
    {
        await using var host = await Start("--Transport:AllowedOrigins:0=https://approved.example.test");
        foreach (var origin in new[] { "https://evil.example.test", "https://approved.example.test" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/institution/admin/members");
            request.Headers.Add("Origin", origin); request.Headers.Add("Access-Control-Request-Method", "GET");
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(origin.Contains("approved"), response.Headers.Contains("Access-Control-Allow-Origin"));
        }
        using var spoof = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        spoof.Headers.Add("X-Forwarded-Proto", "https"); spoof.Headers.Add("X-Forwarded-For", "203.0.113.1");
        spoof.Headers.Add("X-Correlation-ID", "client-controlled");
        using var result = await host.Client.SendAsync(spoof);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.False(result.Headers.Contains("Strict-Transport-Security"));
        Assert.NotEqual("client-controlled", result.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task SensitivePostRateLimitCannotBeEvadedWithForwardedIpAndDoesNotBlockHealth()
    {
        await using var host = await Start("--Transport:SensitivePermitLimit=3");
        for (var i = 0; i < 4; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/institution/admin/invitations");
            request.Headers.Add("X-Forwarded-For", "203.0.113." + (i + 1));
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(i == 3 ? HttpStatusCode.TooManyRequests : HttpStatusCode.Unauthorized, response.StatusCode);
            if (i == 3) Assert.NotNull(response.Headers.RetryAfter);
        }
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task StrictStagingHonorsOnlyConfiguredProxyAndRedirectsUnencryptedRequests()
    {
        // The DB credential is intentionally unusable. This transport-only test
        // opens no SQL connection and does not simulate a working production DB.
        await using var host = await Start("--environment=Staging", "--Deployment:Mode=Staging", "--AllowedHosts=api.staging.example.test",
            "--Jwt:Key=" + new string('x', 64), "--Transport:KnownProxies:0=127.0.0.1", "--Transport:HttpsPort=443",
            "--Readiness:ExpectedVersion=5.7.24", "--Readiness:ExpectedHost=" + System.Environment.MachineName,
            "--Readiness:ExpectedDataDir=not-an-operational-deployment",
            "--ConnectionStrings:DefaultConnection=Server=127.0.0.1;Port=13306;Database=electronic_register_test;User ID=unusable-transport-test;Password=not-a-real-credential;SslMode=VerifyFull;SslCa=owner-ca.pem");
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = host.Client.BaseAddress };
        using var clear = new HttpRequestMessage(HttpMethod.Get, "/health/live"); clear.Headers.Host = "api.staging.example.test";
        using var redirected = await client.SendAsync(clear);
        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirected.StatusCode);
        Assert.Equal("https", redirected.Headers.Location!.Scheme);
        using var forwarded = new HttpRequestMessage(HttpMethod.Get, "/health/live"); forwarded.Headers.Host = "api.staging.example.test";
        forwarded.Headers.Add("X-Forwarded-Proto", "https");
        using var secure = await client.SendAsync(forwarded);
        Assert.Equal(HttpStatusCode.OK, secure.StatusCode);
        Assert.Contains("max-age=15552000", secure.Headers.GetValues("Strict-Transport-Security"));
    }
}
