using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// Dedicated gate: no fallback, EnsureCreated, seed, cleanup or skipped PASS.
public sealed class Mysql84ReleaseTests
{
    [Fact]
    [Trait("Category", "MySQL84Release")]
    public async Task PublishedReleaseAndEmbeddedOnboardingValidationRunOnExactServer()
    {
        var configPath = Environment.GetEnvironmentVariable("ER_MYSQL84_RELEASE_CONFIG")
            ?? throw new InvalidOperationException("Explicit disposable MySQL 8.4 release config required.");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var config = document.RootElement;
        Assert.Equal("true", Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
        Assert.True(config.GetProperty("isolatedCI").GetBoolean());
        Assert.Equal("127.0.0.1", config.GetProperty("host").GetString());
        Assert.StartsWith("er84_", config.GetProperty("database").GetString());
        var port = config.GetProperty("port").GetUInt32();
        Assert.DoesNotContain(port, new uint[] { 3306, 13306 });
        var connectionString = new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1", Port = port, Database = config.GetProperty("database").GetString(),
            UserID = "root", Password = "", SslMode = MySqlSslMode.Disabled
        };
        await using var connection = new MySqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync();
        await using (var identity = new MySqlCommand("SELECT VERSION(),@@server_uuid,(SELECT COUNT(*) FROM er_schema_release WHERE version='er-quality-2-mysql84')", connection))
        await using (var reader = await identity.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("8.4.13", reader.GetString(0));
            Assert.Equal(config.GetProperty("serverUuid").GetString(), reader.GetString(1));
            Assert.Equal(1, reader.GetInt32(2));
        }
        var type = typeof(ElectronicRegisterContext).Assembly.GetType(
            "ElectronicRegisterAPI.Infrastructure.Persistence.Migrations.Mt5cOnboardingIntegrity", throwOnError: true)!;
        var method = type.GetMethod("ValidateIfPresentAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var result = await (Task<string[]>)method.Invoke(null, [connection, null])!;
        Assert.Equal(4, result.Length);
        // Execute through MySqlConnector with text parameters: ASCII case,
        // absolute end, Unicode and NULL are all tested on the real engine.
        foreach (var sample in new string?[] { new('A', 64), new('a', 64), new('A', 63), new('A', 65), "G" + new string('A', 63), "é" + new string('A', 63), new string('A', 64) + "\n", null })
        {
            await using var command = new MySqlCommand(
                "SELECT REGEXP_LIKE(CONVERT(@hash USING utf8mb4) COLLATE utf8mb4_bin, '^[A-F0-9]{64}\\\\z', 'c')", connection);
            command.Parameters.Add("@hash", MySqlDbType.VarChar).Value = (object?)sample ?? DBNull.Value;
            var actual = await command.ExecuteScalarAsync();
            if (sample is null) Assert.Equal(DBNull.Value, actual);
            else Assert.Equal(sample == new string('A', 64) ? 1 : 0, Convert.ToInt32(actual));
        }
        await Mysql84ReleaseHttp.RunAsync(Environment.GetEnvironmentVariable("ER_MYSQL84_EVIDENCE")
            ?? throw new InvalidOperationException("Runtime evidence directory required."), config.GetProperty("serverUuid").GetString()!);
    }
}
