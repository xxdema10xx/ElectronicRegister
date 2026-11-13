using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// Proves that the owned 8.4 purge capability is session scoped: a pooled
// connection never hands it to the next opener, and the application data path
// (same connection string, same pool) never observes it.
[Collection("Mt5aIdentityOnly")]
public sealed class Mysql84CapabilityScopeTests
{
    private static string OwnedConnection()
    {
        var connection = IntegrationFixture.ValidateLocalConnectionString(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION")
            ?? throw new InvalidOperationException("Explicit disposable MySQL 8.4 fixture required"));
        Assert.True(Mysql84FixtureTarget.IsOwned(connection), "Owned MySQL 8.4 fixture required");
        Assert.True(new MySqlConnectionStringBuilder(connection).Pooling);
        return connection;
    }

    private static async Task<(long Id, object? Capability)> Session(MySqlConnection c)
    {
        await using var cmd = new MySqlCommand("SELECT CONNECTION_ID(), @er_mt5b_fixture_capability", c);
        await using var r = await cmd.ExecuteReaderAsync(); Assert.True(await r.ReadAsync());
        return (r.GetInt64(0), r.IsDBNull(1) ? null : r.GetValue(1));
    }

    private static async Task<string> Grant(MySqlConnection c)
    {
        Assert.True(await Mysql84OwnedSession.PrepareAsync(c));
        var capability = await Mysql84FixtureTarget.PurgeCapabilityAsync(c) ?? throw new InvalidOperationException("Capability unavailable");
        Assert.Equal(capability, (await Session(c)).Capability);
        return capability;
    }

    [Fact]
    public async Task ReopenedPooledConnectionNeverKeepsTheCapability()
    {
        var connection = OwnedConnection();
        long first;
        await using (var c = new MySqlConnection(connection)) { await c.OpenAsync(); await Grant(c); first = (await Session(c)).Id; }
        await using var reopened = new MySqlConnection(connection); await reopened.OpenAsync();
        var session = await Session(reopened);
        // Same physical session proves the pool was actually reused, so NULL comes from the reset.
        Assert.Equal(first, session.Id);
        Assert.Null(session.Capability);
    }

    [Fact]
    public async Task ApplicationDataPathNeverObservesTheCapability()
    {
        var connection = OwnedConnection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "ConnectionStrings:DefaultConnection", connection } }).Build();
        await using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
        async Task<(long Id, object? Capability)> Application()
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
            await db.Database.OpenConnectionAsync();
            try { return await Session((MySqlConnection)db.Database.GetDbConnection()); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
        long holder;
        await using (var c = new MySqlConnection(connection))
        {
            await c.OpenAsync(); await Grant(c); holder = (await Session(c)).Id;
            var concurrent = await Application();
            Assert.NotEqual(holder, concurrent.Id);
            Assert.Null(concurrent.Capability);
        }
        // After release the application path may receive the very same physical session: it must be clean.
        var reused = await Application();
        Assert.Null(reused.Capability);
        var sawHolder = reused.Id == holder;
        for (var i = 0; i < 8 && !sawHolder; i++) { var next = await Application(); Assert.Null(next.Capability); sawHolder = next.Id == holder; }
        Assert.True(sawHolder, "The released capability session was never reused by the application path; pool reset not proven");
    }
}
