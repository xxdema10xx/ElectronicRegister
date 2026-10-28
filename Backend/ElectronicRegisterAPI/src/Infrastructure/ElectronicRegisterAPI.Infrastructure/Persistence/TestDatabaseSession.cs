using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

/// <summary>Explicit session-only test setup. Never changes server/global settings.</summary>
public sealed class TestDatabaseSession : DbConnectionInterceptor
{
    private static long _initializations;
    public static long Initializations => Interlocked.Read(ref _initializations);
    public static bool Enabled => Environment.GetEnvironmentVariable("ER_TEST_STRICT_SESSION") == "1";

    private static void Validate(DbConnection connection)
    {
        var b = new MySqlConnectionStringBuilder(connection.ConnectionString);
        if (b.Database != "electronic_register_test" || b.Server is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Test session setup requires the explicit loopback test database.");
    }

    public static async Task ConfigureAsync(DbConnection connection, CancellationToken ct = default)
    {
        if (!Enabled)
            return;
        Validate(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = "SET SESSION sql_mode = CONCAT_WS(',', @@SESSION.sql_mode, 'STRICT_TRANS_TABLES')";
        await command.ExecuteNonQueryAsync(ct);
        Interlocked.Increment(ref _initializations);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (!Enabled)
            return;
        Validate(connection);
        using var command = connection.CreateCommand();
        command.CommandText = "SET SESSION sql_mode = CONCAT_WS(',', @@SESSION.sql_mode, 'STRICT_TRANS_TABLES')";
        command.ExecuteNonQuery();
        Interlocked.Increment(ref _initializations);
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) => ConfigureAsync(connection, cancellationToken);
}
