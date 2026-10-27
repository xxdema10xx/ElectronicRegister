using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ElectronicRegisterAPI.Tests.Integration;

// Test teardown capability only. No production code calls this helper.
internal sealed class MembershipFixturePurge(DbConnection connection, bool close, string? previous) : IAsyncDisposable
{
    internal static async Task<MembershipFixturePurge> AllowAsync(ElectronicRegisterContext db, IEnumerable<Guid> owners)
    {
        var close = db.Database.GetDbConnection().State != ConnectionState.Open;
        if (close) await db.Database.OpenConnectionAsync();
        return await AllowAsync(db.Database.GetDbConnection(), owners, close);
    }
    internal static async Task<MembershipFixturePurge> AllowAsync(DbConnection c, IEnumerable<Guid> owners, bool close = false)
    {
        var ids = owners.Distinct().ToArray();
        if (ids.Any(id => id == Guid.Empty)) throw new ArgumentException("Explicit fixture owners required");
        var target = new MySqlConnectionStringBuilder(c.ConnectionString);
        if (target.Database != "electronic_register_test" || target.Port != TestMySqlTarget.Port || target.Server != "127.0.0.1")
            throw new InvalidOperationException("Fixture purge requires isolated 13306");
        await using var identity = c.CreateCommand();
        identity.CommandText = "SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE()";
        await using(var r = await identity.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync() || r.GetString(0) != "5.7.24" || r.GetInt32(1) != TestMySqlTarget.Port ||
                !r.GetString(2).EndsWith(TestMySqlTarget.DataDirectorySuffix, StringComparison.OrdinalIgnoreCase) ||
                r.GetString(3) != "electronic_register_test") throw new InvalidOperationException("Wrong fixture target");
        }
        await using var read = c.CreateCommand(); read.CommandText = "SELECT @er_mt5b_fixture_owners";
        var old = await read.ExecuteScalarAsync();
        var previous = old is null or DBNull ? null : (string)old;
        await using var set = c.CreateCommand(); set.CommandText = "SET @er_mt5b_fixture_owners=@owners";
        var value = set.CreateParameter(); value.ParameterName = "@owners"; value.Value = string.Join(',', ids); set.Parameters.Add(value);
        await set.ExecuteNonQueryAsync();
        return new(c, close, previous);
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var clear = connection.CreateCommand(); clear.CommandText = "SET @er_mt5b_fixture_owners=@prior";
            var value = clear.CreateParameter(); value.ParameterName = "@prior"; value.Value = (object?)previous ?? DBNull.Value; clear.Parameters.Add(value);
            await clear.ExecuteNonQueryAsync();
        }
        finally { if (close) await connection.CloseAsync(); }
    }
}
