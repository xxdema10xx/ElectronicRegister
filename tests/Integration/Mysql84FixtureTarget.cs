using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MySqlConnector;
using Testcontainers.MySql;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// Only handles containers created by this process. Environment variables can
// never authorize an arbitrary 8.4 server or relax the historical MAMP checks.
internal static class Mysql84FixtureTarget
{
    private sealed record Identity(string Uuid, string Host, uint ServerPort, string DataDir, [property: JsonIgnore] string Capability);
    private static readonly ConcurrentDictionary<string, Identity> Owned = new();
    private static readonly ConcurrentDictionary<string, string> Containers = new();
    internal static object AdministrativeTarget(string connection)
    {
        if (!Owned.TryGetValue(Key(connection), out var identity))
            return new { Port = TestMySqlTarget.Port, ExpectedDataDir = TestMySqlTarget.DataDirectorySuffix };
        return new { Port = new MySqlConnectionStringBuilder(connection).Port, ExpectedDataDir = identity.DataDir,
            LocalMysql84 = new { ContainerId = Containers[Key(connection)], ServerUuid = identity.Uuid, ServerHost = identity.Host } };
    }
    internal static bool IsOwned(string connection)
    {
        try { return Owned.ContainsKey(Key(connection)); }
        catch (ArgumentException) { return false; }
    }
    internal static void Forget(string connection) => Owned.TryRemove(Key(connection), out _);
    internal static async Task<string?> PurgeCapabilityAsync(MySqlConnection connection)
    {
        if(!Owned.TryGetValue(Key(connection.ConnectionString),out var identity)) return null;
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_CONTEXT")), "Only local owned fixtures may purge");
        Assert.True(await VerifyAsync(connection));
        return identity.Capability;
    }
    internal static string[] ReadinessArguments(string connection)
    {
        if (!Owned.TryGetValue(Key(connection), out var identity)) return [];
        return ["--Readiness:ExpectedVersion=8.4.13", "--Readiness:ExpectedHost=" + identity.Host,
            "--Readiness:ExpectedDataDir=" + identity.DataDir,
            "--Readiness:ExpectedServerPort=" + identity.ServerPort];
    }
    private static string Key(string connection)
    {
        var b = new MySqlConnectionStringBuilder(connection);
        return $"{b.Server}:{b.Port}/{b.Database}";
    }

    internal static async Task<bool> VerifyAsync(MySqlConnection c)
    {
        if (!Owned.TryGetValue(Key(c.ConnectionString), out var expected)) return false;
        Assert.Equal("root", new MySqlConnectionStringBuilder(c.ConnectionString).UserID);
        await using var cmd = new MySqlCommand("SELECT VERSION(),@@server_uuid,@@hostname,@@port,@@datadir,DATABASE()", c);
        await using var r = await cmd.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        Assert.Equal("8.4.13", r.GetString(0)); Assert.Equal(expected.Uuid, r.GetString(1));
        Assert.Equal(expected.Host, r.GetString(2)); Assert.Equal(expected.ServerPort, r.GetUInt32(3));
        Assert.Equal(expected.DataDir, r.GetString(4)); Assert.Equal("electronic_register_test", r.GetString(5));
        Assert.Equal("ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION",
            await SqlModeAsync(c, r));
        return true;
    }

    private static async Task<string> SqlModeAsync(MySqlConnection c, MySqlDataReader reader)
    {
        await reader.DisposeAsync();
        await using var command = new MySqlCommand("SELECT @@global.sql_mode", c);
        return Convert.ToString(await command.ExecuteScalarAsync())!;
    }

    internal static async Task ProvisionAsync(MySqlContainer container, string connection)
    {
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_CONTEXT")), "Only local Docker fixtures may issue capabilities");
        var b = new MySqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", b.Server); Assert.Equal("electronic_register_test", b.Database);
        Assert.DoesNotContain(b.Port, new uint[] { 3306, 3307, 13306 });
        await using var c = new MySqlConnection(connection); await c.OpenAsync();
        Identity identity;
        await using (var cmd = new MySqlCommand("SELECT VERSION(),@@server_uuid,@@hostname,@@port,@@datadir", c))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            Assert.True(await r.ReadAsync()); Assert.Equal("8.4.13", r.GetString(0));
            Assert.Equal(container.Id[..12],r.GetString(2));
            identity = new(r.GetString(1), r.GetString(2), r.GetUInt32(3), r.GetString(4),Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools", "mysql84-release.cjs"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Release sources unavailable");
        var work = Path.Combine(root.FullName, ".diagnostics", "fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        await File.WriteAllTextAsync(Path.Combine(work, "server-identity.json"), JsonSerializer.Serialize(new {
            container = container.Id, image = "mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b",
            host = b.Server, port = b.Port, database = b.Database, server = identity,
            retained = Environment.GetEnvironmentVariable("ER_TEST_MYSQL84_RETAIN") == "1"
        }));
        var privateWork = OperatingSystem.IsWindows() ? PrivateTestFiles.CreateDirectory("mysql84-binding") : work;
        var config = Path.Combine(privateWork, "binding.json"); var bundle = Path.Combine(privateWork, "bundle");
        await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new { database = b.Database,host=b.Server,port=(int)b.Port,
            ownedFixture=new { uuid=identity.Uuid,host=identity.Host,capability=identity.Capability },
            definerUser = "fixture_definer", definerHost = "%", apiUser = "fixture_api", apiHost = "%", reviewUser = "fixture_review", reviewHost = "%" }));
        var start = new ProcessStartInfo("node") { WorkingDirectory = root.FullName, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "tools/mysql84-release.cjs", "bundle", config, bundle }) start.ArgumentList.Add(arg);
        using (var process = Process.Start(start) ?? throw new InvalidOperationException("node unavailable"))
        {
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)); }
            catch { process.Kill(entireProcessTree: true); throw; }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
        }
        await using (var cmd = new MySqlCommand("ALTER DATABASE electronic_register_test CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE USER 'fixture_api'@'%' IDENTIFIED BY 'synthetic-fixture-only'; CREATE USER 'fixture_review'@'%' IDENTIFIED BY 'synthetic-fixture-only';", c))
            await cmd.ExecuteNonQueryAsync();
        // Use the actual mysql parser for reviewed DELIMITER scripts. Chunked
        // base64 avoids shell quoting SQL and the Linux per-argument size limit.
        foreach (var file in Directory.GetFiles(bundle, "*.sql").OrderBy(x => x, StringComparer.Ordinal))
        {
            var encoded = Convert.ToBase64String(await File.ReadAllBytesAsync(file));
            await Exec("umask 077; : > /tmp/er-fixture.b64");
            for (var offset = 0; offset < encoded.Length; offset += 24000)
                await Exec("printf '%s' '" + encoded.Substring(offset, Math.Min(24000, encoded.Length-offset)) + "' >> /tmp/er-fixture.b64");
            await Exec("base64 -d /tmp/er-fixture.b64 > /tmp/er-fixture.sql; MYSQL_PWD=test mysql --user=root --default-character-set=utf8mb4 < /tmp/er-fixture.sql");
        }
        Owned[Key(connection)] = identity;
        Containers[Key(connection)] = container.Id;
        async Task Exec(string script)
        {
            var result = await container.ExecAsync(new[] { "sh", "-c", script }).WaitAsync(TimeSpan.FromSeconds(90));
            Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr);
        }
    }
}

public sealed class Mysql84IdentityFixture : IAsyncLifetime
{
    private IntegrationFixture? fixture;
    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable(IntegrationFixture.ConnectionEnvVar) is not null) return;
        if (Environment.GetEnvironmentVariable("ER_TEST_MYSQL84") != "1") return;
        fixture = new IntegrationFixture(); await fixture.InitializeAsync();
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        Environment.SetEnvironmentVariable(IntegrationFixture.ConnectionEnvVar, fixture.ConnectionString);
    }
    public async Task DisposeAsync()
    {
        if (fixture is null) return;
        Environment.SetEnvironmentVariable(IntegrationFixture.ConnectionEnvVar, null);
        await fixture.DisposeAsync();
    }
}
