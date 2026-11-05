using System;
using System.Threading.Tasks;
using MySqlConnector;

namespace ElectronicRegisterAPI.Tests.Integration;

// Test-only session preparation for the owned MySQL 8.4 fixture. On the 8.4
// bundle the fixture purge guards replace the 5.7 identity predicate
// (VERSION()='5.7.24' AND @@port=13306, plus the MAMP datadir) with the attested
// container identity and @er_mt5b_fixture_capability (tools/mysql84-fixture.cjs).
// Setting the capability here is the 8.4 equivalent of being connected to the
// verified 5.7 server: the explicit @er_mt5b_fixture_owners requirement is
// unchanged. The variable is session scoped and cleared by the pool reset
// (Mysql84CapabilityScopeTests), so the application never observes it.
internal static class Mysql84OwnedSession
{
    internal static async Task<bool> PrepareAsync(MySqlConnection c)
    {
        if (!await Mysql84FixtureTarget.VerifyAsync(c)) return false;
        var capability = await Mysql84FixtureTarget.PurgeCapabilityAsync(c) ?? throw new InvalidOperationException("Owned fixture capability unavailable");
        await using var set = new MySqlCommand("SET @er_mt5b_fixture_capability=@cap", c);
        set.Parameters.AddWithValue("@cap", capability);
        await set.ExecuteNonQueryAsync();
        return true;
    }
}
