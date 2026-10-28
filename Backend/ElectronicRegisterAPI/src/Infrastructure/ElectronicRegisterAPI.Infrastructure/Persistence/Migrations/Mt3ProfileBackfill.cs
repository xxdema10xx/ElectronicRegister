using System.Reflection;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Test-only explicit migration utility, never registered in DI or called at startup.
public static class Mt3ProfileBackfill
{
    public static async Task ExecuteAsync(string connectionString, Guid legacyInstitutionId)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (builder.Database != "electronic_register_test" || legacyInstitutionId == Guid.Empty)
            throw new ArgumentException("Explicit authorized test database and legacy holding required.");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        if ((string? )await new MySqlCommand("SELECT DATABASE()", connection).ExecuteScalarAsync() != "electronic_register_test")
            throw new InvalidOperationException("Wrong database.");
        await using var transaction = await connection.BeginTransactionAsync();
        async Task<long> Count(string sql)
        {
            await using var command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@legacy_id", legacyInstitutionId);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        if (await Count("SELECT COUNT(*) FROM institution WHERE id=@legacy_id AND code='LEGACY_MT1'") != 1)
            throw new InvalidOperationException("Explicit MT1 migration holding not present.");
        foreach (var kind in new[]
        {
            "student",
            "teacher"
        }

        )
        {
            // Identifiers are a closed internal allowlist, not client input.
            var role = kind == "student" ? "Student" : "Teacher";
            if (await Count($"SELECT COUNT(*) FROM user u WHERE u.{kind}_id IS NOT NULL AND (u.role<>'{kind}' OR NOT EXISTS " + $"(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id WHERE m.institution_id=@legacy_id AND m.user_id=u.id AND r.role='{role}'))") != 0)
                throw new InvalidOperationException("Ambiguous/incompatible legacy profile. No silent assignment.");
            if (await Count($"SELECT COUNT(*) FROM user u JOIN institution_{kind}_profile p ON p.{kind}_id=u.{kind}_id WHERE p.institution_id<>@legacy_id") != 0)
                throw new InvalidOperationException("Existing institutional profile conflicts with explicit legacy manifest.");
        }

        // MT3A scopes legacy linking to profiles actually owned by the manifest.
        // Original 002 remains immutable historical SQL; no cross-tenant reassignment.
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MT3.008_owned_profile_backfill.sql") ?? throw new InvalidOperationException("Canonical backfill resource missing.");
        var sql = await new StreamReader(stream).ReadToEndAsync();
        await using var insert = new MySqlCommand(sql, connection, transaction);
        insert.Parameters.AddWithValue("@legacy_id", legacyInstitutionId);
        await insert.ExecuteNonQueryAsync(); // Duplicate/FK conflict aborts the entire DML transaction.
        await transaction.CommitAsync();
    }
}
