using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

namespace ElectronicRegisterAPI.Tests.Integration;

// Decision D-B (er-platform-identity-1): User deletion and email changes are rejected by
// platform_user_delete/platform_user_update. Only an attested owned test container, for
// explicitly listed synthetic users with *.example.test addresses, may perform them.
// Test teardown and legacy fixture setup only; no production code calls this helper.
internal static class FixtureIdentityExemption
{
    internal static async Task ExecuteAsync(string connectionString, IEnumerable<Guid> users, string sql, params (string Name, object Value)[] values)
    {
        await using var c = new MySqlConnection(new MySqlConnectionStringBuilder(connectionString) { AllowUserVariables = true }.ConnectionString);
        await c.OpenAsync();
        await using var exemption = await MembershipFixturePurge.AllowAsync(c, users);
        await using var command = new MySqlCommand(sql, c);
        foreach (var (name, value) in values)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
