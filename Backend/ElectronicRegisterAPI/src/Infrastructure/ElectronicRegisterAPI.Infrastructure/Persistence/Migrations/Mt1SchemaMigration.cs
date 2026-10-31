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

    public static async Task BackfillAsync(
        string connectionString,
        Guid legacyInstitutionId,
        string migrationName,
        IReadOnlyList<ElectronicRegisterAPI.Domain.Models.MembershipMigrationAssignment> assignments
    )
    {
        if (legacyInstitutionId == Guid.Empty || string.IsNullOrWhiteSpace(migrationName) || migrationName.Length > 200 || assignments is null || assignments.Select(a => a.UserId).Distinct().Count() != assignments.Count || assignments.Any(a => a.UserId == Guid.Empty || a.Roles is null || a.Roles.Count is < 1 or > 4 || a.Roles.Distinct().Count() != a.Roles.Count || a.Roles.Any(r => !Enum.IsDefined(r))))
            throw new ArgumentException("Explicit migration identity, user IDs and institution roles are required.");
        await using var connection = await OpenAuthorizedAsync(connectionString);
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var conflict = new MySqlCommand("SELECT COUNT(*) FROM institution WHERE (id=@id OR code=@code) AND (id<>@id OR code<>@code OR name<>@name OR status<>'provisioning')", connection, transaction);
            Parameters(conflict, legacyInstitutionId, migrationName);
            if (Convert.ToInt64(await conflict.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Legacy migration identity conflicts with an existing institution.");
            await using var holding = new MySqlCommand("INSERT INTO institution(id,name,code,status) SELECT @id,@name,@code,'provisioning' WHERE NOT EXISTS(SELECT 1 FROM institution WHERE id=@id)", connection, transaction);
            Parameters(holding, legacyInstitutionId, migrationName);
            await holding.ExecuteNonQueryAsync();
            await using var mutex = new MySqlCommand("SELECT id FROM institution WHERE id=@id FOR UPDATE", connection, transaction);
            mutex.Parameters.AddWithValue("@id", legacyInstitutionId);
            await mutex.ExecuteScalarAsync();
            foreach (var assignment in assignments.OrderBy(a => a.UserId.ToString("D"), StringComparer.Ordinal))
            {
                await using var user = new MySqlCommand("SELECT id FROM `user` WHERE id=@user FOR UPDATE", connection, transaction);
                user.Parameters.AddWithValue("@user", assignment.UserId);
                if (await user.ExecuteScalarAsync()is null)
                    throw new InvalidOperationException("Explicit migration account does not exist.");
                await using var find = new MySqlCommand("SELECT id FROM institution_membership WHERE institution_id=@institution AND user_id=@user FOR UPDATE", connection, transaction);
                find.Parameters.AddWithValue("@institution", legacyInstitutionId);
                find.Parameters.AddWithValue("@user", assignment.UserId);
                var existing = await find.ExecuteScalarAsync();
                var membership = existing is null ? Guid.NewGuid() : Guid.Parse(existing.ToString()!);
                var expected = assignment.Roles.Select(r => r.ToString()).Order(StringComparer.Ordinal).ToArray();
                if (existing is not null)
                {
                    var actual = new List<string>();
                    await using var roles = new MySqlCommand("SELECT role FROM institution_membership_role WHERE membership_id=@member ORDER BY role FOR UPDATE", connection, transaction);
                    roles.Parameters.AddWithValue("@member", membership);
                    await using var reader = await roles.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        actual.Add(reader.GetString(0));
                    if (!actual.Order(StringComparer.Ordinal).SequenceEqual(expected))
                        throw new InvalidOperationException("Backfill will not overwrite existing membership roles.");
                    continue;
                }

                await using var insert = new MySqlCommand("INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES(@member,@institution,@user,'active')", connection, transaction);
                insert.Parameters.AddWithValue("@member", membership);
                insert.Parameters.AddWithValue("@institution", legacyInstitutionId);
                insert.Parameters.AddWithValue("@user", assignment.UserId);
                await insert.ExecuteNonQueryAsync();
                foreach (var role in expected)
                {
                    await using var grant = new MySqlCommand("INSERT INTO institution_membership_role(membership_id,role) VALUES(@member,@role)", connection, transaction);
                    grant.Parameters.AddWithValue("@member", membership);
                    grant.Parameters.AddWithValue("@role", role);
                    await grant.ExecuteNonQueryAsync();
                }
            }

            // No scan of user.role, inferred role conversion, global account expansion,
            // account/profile rewrite or operational institution activation.
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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
            if (!string.Equals((string? )await command.ExecuteScalarAsync(), builder.Database, StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected active database.");
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static IEnumerable<string> Statements(string name)
    {
        using var stream = typeof(Mt1SchemaMigration).Assembly.GetManifestResourceStream("MT1." + name) ?? throw new InvalidOperationException("Missing canonical MT-1 SQL resource.");
        using var reader = new StreamReader(stream);
        // Canonical scripts have no routines or semicolons inside literals. Reject drift during review.
        var sql = string.Join('\n', reader.ReadToEnd().Split('\n').Where(line => !line.TrimStart().StartsWith("--")));
        return sql.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
