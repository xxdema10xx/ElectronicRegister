using System;
using System.Threading.Tasks;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

internal static class DiagnosticPreflight
{
    internal static async Task Check(string connection)
    {
        await using var c = new MySqlConnection(connection); await c.OpenAsync();
        if (!await Mysql84FixtureTarget.VerifyAsync(c))
            throw new InvalidOperationException("Diagnostic preflight requires a process-owned MySQL 8.4.13 container");
        await using var schema = new MySqlCommand("CALL er_schema_ready()", c);
        Assert.Equal(1, Convert.ToInt32(await schema.ExecuteScalarAsync()));
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(connection, Guid.NewGuid(), [], enabled: false);
        Assert.NotNull(host.Host);
    }
}

[Collection(IntegrationCollection.Name)]
public sealed class DiagnosticPreflightTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task SchemaConnectionAndHost()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await DiagnosticPreflight.Check(fixture.ConnectionString);
    }
}

[Collection("Mt5aIdentityOnly")]
public sealed class IdentityDiagnosticPreflightTests
{
    [Fact]
    public Task SchemaConnectionAndHost() => DiagnosticPreflight.Check(
        Environment.GetEnvironmentVariable(IntegrationFixture.ConnectionEnvVar) ?? throw new InvalidOperationException("Identity fixture unavailable"));
}
