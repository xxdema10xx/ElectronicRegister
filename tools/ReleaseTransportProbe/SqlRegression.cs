using System.Data;
using MySqlConnector;

internal static class SqlRegression
{
    internal static async Task<object> Run(MySqlConnectionStringBuilder settings)
    {
        await using var first = new MySqlConnection(settings.ConnectionString);
        await using var second = new MySqlConnection(settings.ConnectionString);
        await first.OpenAsync();
        await second.OpenAsync();
        static async Task<object?> Query(MySqlConnection c, MySqlTransaction? t, string sql)
        {
            await using var command = new MySqlCommand(sql, c, t);
            return await command.ExecuteScalarAsync();
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        var grants = new List<string>();
        await using (var command = new MySqlCommand("SHOW GRANTS", first))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) grants.Add(reader.GetString(0));
        Require(grants.Count == 3 && grants.All(g => !g.Contains("WITH GRANT OPTION", StringComparison.Ordinal)) &&
            grants.Count(g => g.StartsWith("GRANT USAGE ON *.* TO ", StringComparison.Ordinal)) == 1 &&
            grants.Count(g => g.StartsWith("GRANT SELECT, INSERT, UPDATE, DELETE ON `electronic_register_staging`.* TO ", StringComparison.Ordinal)) == 1 &&
            grants.Count(g => g.StartsWith("GRANT EXECUTE ON PROCEDURE `electronic_register_staging`.`er_schema_ready` TO ", StringComparison.Ordinal)) == 1,
            "Unexpected API grant, role or administrative capability.");
        // Real least-privilege session: metadata absence has a different meaning
        // for TRIGGERS than for tables/FKs. Check this before starting HTTP.
        const string calendar = "'institution_calendar_settings','institution_calendar_period','institution_lesson','institution_calendar_event','institution_calendar_import','institution_calendar_occupancy'";
        Require(Convert.ToInt32(await Query(first, null, $"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN({calendar})")) == 6, "API calendar table visibility mismatch.");
        Require(Convert.ToInt32(await Query(first, null, $"SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND EVENT_OBJECT_TABLE IN({calendar})")) == 0, "API must not have trigger metadata visibility.");
        Require(Convert.ToInt32(await Query(first, null, $"SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN({calendar})")) == 22, "API calendar FK visibility mismatch.");
        Require(Convert.ToInt32(await Query(first, null, "SELECT COUNT(*) FROM (SELECT DISTINCT TABLE_NAME,CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME='class') AS dependencies")) == 8, "API class dependency visibility mismatch.");
        Require(Convert.ToInt32(await Query(first, null, "CALL er_schema_ready()")) == 1, "Privileged schema attestation failed.");
        Require(Convert.ToString(await Query(first, null, "SELECT @@transaction_isolation")) == "REPEATABLE-READ", "Unexpected default isolation.");
        var marker = "probe-" + Guid.NewGuid().ToString("N");
        await using (var transaction = await first.BeginTransactionAsync(IsolationLevel.RepeatableRead))
        {
            await Query(first, transaction, $"INSERT INTO er_schema_release(version) VALUES('{marker}')");
            Require(Convert.ToInt32(await Query(second, null, $"SELECT COUNT(*) FROM er_schema_release WHERE version='{marker}'")) == 0, "Uncommitted row leaked.");
            await transaction.RollbackAsync();
        }
        Require(Convert.ToInt32(await Query(second, null, $"SELECT COUNT(*) FROM er_schema_release WHERE version='{marker}'")) == 0, "Rollback failed.");
        // Existing readiness markers are locked without changing their values.
        const string one = "UPDATE er_schema_release SET version=version WHERE version='er-quality-1'";
        const string two = "UPDATE er_schema_release SET version=version WHERE version='er-quality-1-closure-1'";
        await using (var a = await first.BeginTransactionAsync())
        await using (var b = await second.BeginTransactionAsync())
        {
            await Query(first, a, one);
            await Query(second, b, two);
            async Task<bool> Cross(MySqlConnection c, MySqlTransaction t, string sql)
            {
                try { await Query(c, t, sql); return false; }
                catch (MySqlException error) when (error.Number == 1213) { return true; }
            }
            var victims = await Task.WhenAll(Cross(first, a, two), Cross(second, b, one));
            Require(victims.Count(v => v) == 1, "Expected exactly one InnoDB deadlock victim.");
            // Disposal rolls back the survivor; the victim has already been rolled back by InnoDB.
        }
        await Query(second, null, "SET SESSION innodb_lock_wait_timeout=1");
        await using (var a = await first.BeginTransactionAsync())
        await using (var b = await second.BeginTransactionAsync())
        {
            await Query(first, a, one);
            var timedOut = false;
            try { await Query(second, b, one); }
            catch (MySqlException error) when (error.Number == 1205) { timedOut = true; }
            Require(timedOut, "Expected a server lock timeout, not a network error.");
        }
        var denied = false;
        try { await Query(first, null, "CREATE TABLE er_api_ddl_forbidden(id int)"); }
        catch (MySqlException error) when (error.Number == 1142) { denied = true; }
        Require(denied, "API account unexpectedly permits DDL.");
        Require(Convert.ToInt32(await Query(first, null, "CALL er_schema_ready()")) == 1, "Schema changed after rollback probes.");
        return new { isolation = "REPEATABLE-READ", dirtyReadRejected = true, rollback = true, deadlockVictims = 1, lockTimeout = true, apiDdlDenied = true, apiGrantsVerified = true, apiCalendarTables = 6, apiCalendarTriggers = 0, apiCalendarForeignKeys = 22, apiClassForeignKeys = 8, privilegedSchemaAttested = true };
    }
}
