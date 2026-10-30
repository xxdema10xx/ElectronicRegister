using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using System;
using System.IO;
using System.Linq;
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
        public string Selection(Guid userId, long identityGeneration = 0) => throw new InvalidOperationException("synthetic-secret-must-not-be-logged");
        public string Access(TenantContext context, long identityGeneration = 0) => throw new InvalidOperationException();
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
        Assert.Contains(logs.Entries, e => e.Contains("REQUEST_FAILURE", StringComparison.Ordinal) && e.Contains("InvalidOperationException", StringComparison.Ordinal));
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows protected administrative host required.");
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var folder = PrivateTestFiles.CreateDirectory("operator-acl");
        var operation = Operation();
        var options = new JsonSerializerOptions(); options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<PlatformAction>());
        var config = Path.Combine(folder, "admin.json"); var signed = Path.Combine(folder, "signed.json");
        var assembly = typeof(PlatformCliTestChannel).Assembly.Location;
        await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new
        {
            ConnectionStrings = new { DefaultConnection = connection },
            TestDatabase = new { Port = TestMySqlTarget.Port, ExpectedDataDir = TestMySqlTarget.DataDirectorySuffix },
            Multitenancy = new { Onboarding = new { Enabled = true } },
            PlatformAuthority = new { Enabled = true, Keys = new[] { key } },
            TestInbox = folder,
            InvitationChannel = new
            {
                AssemblyPath = assembly,
                SHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(assembly))),
                Type = typeof(PlatformCliTestChannel).FullName
            }
        }, options));
        await File.WriteAllTextAsync(signed, JsonSerializer.Serialize(operation, options));
        var originalAcl = new FileInfo(config).GetAccessControl();
        var acl = new FileInfo(config).GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-1-0"), FileSystemRights.ReadData, AccessControlType.Allow));
        new FileInfo(config).SetAccessControl(acl);
        async Task<(int ExitCode, string Output, string Error)> RunCli()
        {
            var info = new System.Diagnostics.ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "run", "--no-build", "--project", "tools/PlatformAdmin", "--", config, signed }) info.ArgumentList.Add(arg);
            using var child = System.Diagnostics.Process.Start(info)!;
            var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            return (child.ExitCode, await output, await error);
        }
        try
        {
            var readableAcl = new FileInfo(config).GetAccessControl();
            Assert.Contains(readableAcl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>(),
                rule => rule.IdentityReference.Value == "S-1-1-0" && rule.AccessControlType == AccessControlType.Allow &&
                    (rule.FileSystemRights & FileSystemRights.ReadData) != 0);
            var refused = await RunCli();
            Assert.Equal(1, refused.ExitCode); Assert.Empty(refused.Output);
            Assert.DoesNotContain(connection, refused.Error);
            Assert.Equal(0, await Count("SELECT COUNT(*) FROM institution WHERE id=@p0", operation.Intent.InstitutionId));
        }
        finally { PrivateTestFiles.RestoreAccessRules(config, originalAcl); }
        // Same target, signature and channel: only the file ACL has changed.
        var accepted = await RunCli();
        Assert.True(accepted.ExitCode == 0, "Private authority configuration must be accepted; inspect private test evidence.");
        Assert.Equal(operation.Intent.InstitutionId, JsonSerializer.Deserialize<PlatformResult>(accepted.Output)!.InstitutionId);
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM institution WHERE id=@p0", operation.Intent.InstitutionId));
    }
}
