using ElectronicRegisterAPI.Domain.Enums;
using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class Mt5cOnboardingTests
{
    private sealed class FailingTokens : IInstitutionTokens
    {
        public string Selection(Guid userId) => throw new InvalidOperationException("synthetic-secret-must-not-be-logged");
        public string Access(TenantContext context) => throw new InvalidOperationException();
    }
    private sealed class SecurityLogs : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Entries = new();
        public ILogger CreateLogger(string categoryName) => new Collector(Entries);
        public void Dispose() { }
        private sealed class Collector(System.Collections.Concurrent.ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
                => entries.Enqueue(format(state, error) + (error?.ToString() ?? ""));
        }
    }
    [Fact]
    public async Task UnhandledHttpErrorPreservesSecurityHeadersAndNeverLogsExceptionSecretOrQuery()
    {
        var logs = new SecurityLogs();
        await using var server = await Mt3cHosting.ProgramHost.StartAsync(connection, Guid.Empty, [], enabled: false, unbackfilled: false,
            extraArguments: ["--Logging:LogLevel:Default=Information"], configureServices: services =>
            {
                services.AddSingleton<IInstitutionTokens, FailingTokens>();
                services.AddLogging(logging => logging.AddProvider(logs));
            });
        using var response = await server.Client.PostAsJsonAsync("/api/Auth/identity/login?private=synthetic-secret-query", new LoginDto { Email = Email(0), Password = Password });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        foreach (var header in new[] { "X-Correlation-ID", "X-Content-Type-Options", "X-Frame-Options", "Content-Security-Policy" }) Assert.True(response.Headers.Contains(header), header);
        Assert.DoesNotContain("synthetic-secret", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(logs.Entries, e => e.Contains("synthetic-secret", StringComparison.Ordinal) || e.Contains(Password, StringComparison.Ordinal));
        Assert.Contains(logs.Entries, e => e.Contains("Request failed (InvalidOperationException)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows protected administrative host required.");
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var folder = Path.Combine(root, "migrations/mt4/private/mt5a-mamp-13306/cli-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); PrivateTestFiles.ProtectNewDirectory(folder);
        var operation = Operation();
        var options = new JsonSerializerOptions(); options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<PlatformAction>());
        var config = Path.Combine(folder, "admin.json"); var signed = Path.Combine(folder, "signed.json");
        await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new
        {
            ConnectionStrings = new { DefaultConnection = connection }, Multitenancy = new { Onboarding = new { Enabled = true } },
            PlatformAuthority = new { Enabled = true, Keys = new[] { key } }
        }, options));
        await File.WriteAllTextAsync(signed, JsonSerializer.Serialize(operation, options));
        var originalAcl = new FileInfo(config).GetAccessControl();
        var acl = new FileInfo(config).GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-1-0"), FileSystemRights.ReadData, AccessControlType.Allow));
        new FileInfo(config).SetAccessControl(acl);
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "run", "--no-build", "--project", "tools/PlatformAdmin", "--", config, signed }) info.ArgumentList.Add(arg);
            using var child = System.Diagnostics.Process.Start(info)!;
            var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(1, child.ExitCode); Assert.Empty(await output);
            Assert.DoesNotContain(connection, await error);
            Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution WHERE id=@p0", operation.Intent.InstitutionId));
        }
        finally { new FileInfo(config).SetAccessControl(originalAcl); }
    }
}
