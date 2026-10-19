using System.Text.Json;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using MySqlConnector;

var connection = Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION") ?? throw new ArgumentException("TEST_MYSQL_CONNECTION required.");
if (args.Length < 2 || !Guid.TryParse(args[1], out var legacy) || legacy == Guid.Empty) throw new ArgumentException("stage explicit-legacy-guid [backup]");
await using var c = await Mt3aOwnershipMigration.OpenAsync(connection);
switch (args[0])
{
    case "snapshot":
        Console.WriteLine(JsonSerializer.Serialize(await Mt3aOwnershipMigration.SnapshotAsync(c), new JsonSerializerOptions { WriteIndented = true }));
        break;
    case "provision-test-holding":
        // Explicit operator invocation, no automatic membership or identity creation.
        await using (var cmd = new MySqlCommand("INSERT INTO institution(id,name,code,status) VALUES (@id,'MT3A test migration holding - not a verified legal institution','MT3A_LEGACY_TEST','provisioning')", c))
        { cmd.Parameters.AddWithValue("@id", legacy); await cmd.ExecuteNonQueryAsync(); }
        break;
    case "backfill": await Mt3aOwnershipMigration.BackfillAsync(connection, legacy); Console.WriteLine("Backfill committed; every legacy cell digest preserved."); break;
    case "validate": await Mt3aOwnershipMigration.ValidateGraphAsync(c, legacy); Console.WriteLine("Graph validated."); break;
    case "expand": case "enforce": case "audit-platform": case "prune-indexes": case "prune-fk-indexes":
        if (args.Length != 3) throw new ArgumentException("Verified backup path required.");
        await Mt3aOwnershipMigration.ExecuteDdlAsync(connection, args[0], legacy, args[2]);
        Console.WriteLine(args[0] + " completed. DDL is not transactional."); break;
    default: throw new ArgumentException("Unknown stage.");
}
