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
internal sealed class MembershipFixturePurge(DbConnection connection, bool close, string? previous, string? previousCapability=null, bool owned84=false) : IAsyncDisposable
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
        var capability = c is MySqlConnection mysql ? await Mysql84FixtureTarget.PurgeCapabilityAsync(mysql) : null;
        if (target.Database != "electronic_register_test" || target.Server != "127.0.0.1" || (capability is null && target.Port != TestMySqlTarget.Port))
            throw new InvalidOperationException("Fixture purge requires isolated 13306");
        if(capability is null) {
        await using var identity = c.CreateCommand();
        identity.CommandText = "SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE()";
        await using(var r = await identity.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync() || r.GetString(0) != "5.7.24" || r.GetInt32(1) != TestMySqlTarget.Port ||
                !r.GetString(2).EndsWith(TestMySqlTarget.DataDirectorySuffix, StringComparison.OrdinalIgnoreCase) ||
                r.GetString(3) != "electronic_register_test") throw new InvalidOperationException("Wrong fixture target");
        }
        }
        string? priorCapability=null;
        if(capability is not null) {
            await using var get=c.CreateCommand();get.CommandText="SELECT @er_mt5b_fixture_capability";
            priorCapability=await get.ExecuteScalarAsync() as string;
            await using var put=c.CreateCommand();put.CommandText="SET @er_mt5b_fixture_capability=@cap";
            var parameter=put.CreateParameter();parameter.ParameterName="@cap";parameter.Value=capability;put.Parameters.Add(parameter);await put.ExecuteNonQueryAsync();
        }
        await using var read = c.CreateCommand(); read.CommandText = "SELECT @er_mt5b_fixture_owners";
        var old = await read.ExecuteScalarAsync();
        var previous = old is null or DBNull ? null : (string)old;
        await using var set = c.CreateCommand(); set.CommandText = "SET @er_mt5b_fixture_owners=@owners";
        var value = set.CreateParameter(); value.ParameterName = "@owners"; value.Value = string.Join(',', ids); set.Parameters.Add(value);
        await set.ExecuteNonQueryAsync();
        return new(c, close, previous,priorCapability,capability is not null);
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            if(owned84) {
                if(connection is not MySqlConnection mysql || !await Mysql84FixtureTarget.VerifyAsync(mysql))throw new InvalidOperationException("Owned purge identity changed");
                await using var restore=connection.CreateCommand();restore.CommandText="SET @er_mt5b_fixture_capability=@cap";
                var parameter=restore.CreateParameter();parameter.ParameterName="@cap";parameter.Value=(object?)previousCapability??DBNull.Value;restore.Parameters.Add(parameter);await restore.ExecuteNonQueryAsync();
            }
            await using var clear = connection.CreateCommand(); clear.CommandText = "SET @er_mt5b_fixture_owners=@prior";
            var value = clear.CreateParameter(); value.ParameterName = "@prior"; value.Value = (object?)previous ?? DBNull.Value; clear.Parameters.Add(value);
            await clear.ExecuteNonQueryAsync();
        }
        finally { if (close) await connection.CloseAsync(); }
    }
}
