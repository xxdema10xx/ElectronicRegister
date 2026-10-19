using System.Reflection;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Explicit test/migration tooling. Not registered in DI, invoked by Program, or exposed over HTTP.
// MT-1 intentionally refuses every database other than the authorized test database.
public static class Mt1SchemaMigration
{
    public const string LegacyMigrationCode = "LEGACY_MT1";

    public static async Task ExpandAsync(string connectionString)
    {
        await using var connection = await OpenAuthorizedAsync(connectionString);
        foreach (var sql in Statements("001_expand.sql"))
            await new MySqlCommand(sql, connection).ExecuteNonQueryAsync();
    }

    public static async Task BackfillAsync(string connectionString, Guid legacyInstitutionId, string migrationName)
    {
        if (legacyInstitutionId == Guid.Empty || string.IsNullOrWhiteSpace(migrationName) || migrationName.Length > 200)
            throw new ArgumentException("Explicit migration identity and name are required.");
        await using var connection = await OpenAuthorizedAsync(connectionString);
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var conflict = new MySqlCommand("SELECT COUNT(*) FROM institution WHERE (id=@id OR code=@code) " +
                "AND (id<>@id OR code<>@code OR name<>@name OR status<>'provisioning')", connection, transaction);
            Parameters(conflict, legacyInstitutionId, migrationName);
            if (Convert.ToInt64(await conflict.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Legacy migration identity conflicts with an existing institution.");
            var invalidRole = new MySqlCommand("SELECT COUNT(*) FROM user WHERE role NOT IN ('admin','teacher','student') OR role IS NULL", connection, transaction);
            if (Convert.ToInt64(await invalidRole.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Legacy role cannot be mapped safely.");
            var roleConflict = new MySqlCommand("SELECT COUNT(*) FROM institution_membership m JOIN user u ON u.id=m.user_id " +
                "WHERE m.institution_id=@id AND (" +
                "(SELECT COUNT(*) FROM institution_membership_role r WHERE r.membership_id=m.id)>1 OR " +
                "EXISTS(SELECT 1 FROM institution_membership_role r WHERE r.membership_id=m.id AND r.role<>" +
                "CASE u.role WHEN 'admin' THEN 'InstitutionAdmin' WHEN 'teacher' THEN 'Teacher' WHEN 'student' THEN 'Student' END))", connection, transaction);
            Parameters(roleConflict, legacyInstitutionId, migrationName);
            if (Convert.ToInt64(await roleConflict.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Backfill will not overwrite existing membership roles.");
            foreach (var sql in Statements("002_backfill.sql"))
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@legacy_id", legacyInstitutionId);
                command.Parameters.AddWithValue("@legacy_code", LegacyMigrationCode);
                command.Parameters.AddWithValue("@legacy_name", migrationName);
                await command.ExecuteNonQueryAsync();
            }
            foreach (var sql in Statements("003_validate.sql").Skip(1)) // First SELECT is informational user count.
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@legacy_id", legacyInstitutionId);
                if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 0)
                    throw new InvalidOperationException("MT-1 backfill validation failed; DML will roll back.");
            }
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    private static void Parameters(MySqlCommand command, Guid id, string name)
    {
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@code", LegacyMigrationCode);
        command.Parameters.AddWithValue("@name", name);
    }

    private static async Task<MySqlConnection> OpenAuthorizedAsync(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "electronic_register_test", StringComparison.Ordinal))
            throw new InvalidOperationException("MT-1 tooling is restricted to electronic_register_test.");
        var connection = new MySqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync();
            await using var command = new MySqlCommand("SELECT DATABASE()", connection);
            if (!string.Equals((string?)await command.ExecuteScalarAsync(), builder.Database, StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected active database.");
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static IEnumerable<string> Statements(string name)
    {
        using var stream = typeof(Mt1SchemaMigration).Assembly.GetManifestResourceStream("MT1." + name)
            ?? throw new InvalidOperationException("Missing canonical MT-1 SQL resource.");
        using var reader = new StreamReader(stream);
        // Canonical scripts have no routines or semicolons inside literals. Reject drift during review.
        var sql = string.Join('\n', reader.ReadToEnd().Split('\n').Where(line => !line.TrimStart().StartsWith("--")));
        return sql.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
