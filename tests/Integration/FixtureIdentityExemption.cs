using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

    // EF teardowns: a nested exemption for the listed users only; the enclosing purge scope
    // (institution owners) is restored when the exemption is disposed.
    internal static async Task DeleteUsersAsync(ElectronicRegisterContext db, IReadOnlyCollection<Guid> users)
    {
        await using var exemption = await MembershipFixturePurge.AllowAsync(db, users);
        await db.Users.Where(u => users.Contains(u.Id)).ExecuteDeleteAsync();
    }

    internal static async Task DeleteUsersByEmailAsync(ElectronicRegisterContext db, string email)
    {
        var users = await db.Users.Where(u => u.Email == email).Select(u => u.Id).ToListAsync();
        if (users.Count > 0)
            await DeleteUsersAsync(db, users);
    }
}
