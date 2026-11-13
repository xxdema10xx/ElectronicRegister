using System.Collections.Concurrent;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

// Detects the er-platform-identity-1 successor per database. Once installed it is never
// removed (positive answers are cached permanently); a missing marker is re-read after
// 30 seconds. Without the successor no account can be Closed, so identity queries keep
// their previous shape; with it, Closed accounts are always treated as absent.
internal sealed class PlatformIdentitySchema
{
    public const string Marker = "er-platform-identity-1";
    private readonly ConcurrentDictionary<string, bool> _installed = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _absentUntil = new(StringComparer.Ordinal);

    public async Task<bool> InstalledAsync(ElectronicRegisterContext db, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        var builder = new MySqlConnectionStringBuilder(connection.ConnectionString);
        var key = builder.Server + ":" + builder.Port + "/" + builder.Database;
        if (_installed.ContainsKey(key))
            return true;
        if (_absentUntil.TryGetValue(key, out var until) && Environment.TickCount64 < until)
            return false;
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT COUNT(*) FROM er_schema_release WHERE version=@marker";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@marker";
        parameter.Value = Marker;
        command.Parameters.Add(parameter);
        bool installed;
        try
        {
            installed = Convert.ToInt64(await command.ExecuteScalarAsync(ct)) == 1;
        }
        catch (MySqlException error) when (error.Number == 1146)
        {
            installed = false;
        }
        if (installed)
            _installed[key] = true;
        else
            _absentUntil[key] = Environment.TickCount64 + 30_000;
        return installed;
    }
}
