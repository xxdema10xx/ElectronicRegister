using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Classification only: legacy backfill never writes calendar tables.
internal static class CalendarIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection c, MySqlTransaction? tx)
    {
        string[] tables = ["institution_calendar_settings", "institution_calendar_period", "institution_lesson", "institution_calendar_event", "institution_calendar_import", "institution_calendar_occupancy"];
        var names = string.Join(',', tables.Select(t => "'" + t + "'"));
        async Task<long> Scalar(string sql)
        {
            await using var cmd = new MySqlCommand(sql, c, tx);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        var count = await Scalar($"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN({names})");
        if (count == 0)
            return[];
        if (count != 6)
            throw new InvalidOperationException("Incomplete calendar tables: no backfill permitted.");
        const string grantee = "CONCAT(CHAR(39),SUBSTRING_INDEX(CURRENT_USER(),'@',1),CHAR(39),'@',CHAR(39),SUBSTRING_INDEX(CURRENT_USER(),'@',-1),CHAR(39))";
        if (await Scalar($"SELECT (SELECT COUNT(*) FROM information_schema.SCHEMA_PRIVILEGES WHERE GRANTEE={grantee} AND TABLE_SCHEMA=DATABASE() AND PRIVILEGE_TYPE='TRIGGER') + (SELECT COUNT(*) FROM information_schema.USER_PRIVILEGES WHERE GRANTEE={grantee} AND PRIVILEGE_TYPE='TRIGGER')") == 0)
            throw new InvalidOperationException("Calendar schema attestation requires TRIGGER metadata visibility: no backfill permitted.");
        if (await Scalar($"SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND EVENT_OBJECT_TABLE IN({names})") != 19)
            throw new InvalidOperationException("Incomplete calendar triggers: no backfill permitted.");
        if (await Scalar($"SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN({names})") != 22)
            throw new InvalidOperationException("Incomplete calendar foreign keys: no backfill permitted.");
        if (await Scalar("SELECT COUNT(*) FROM institution_lesson l LEFT JOIN formative_module m ON m.institution_id=l.institution_id AND m.version_id=l.version_id AND m.id=l.module_id WHERE m.id IS NULL") != 0)
            throw new InvalidOperationException("Invalid calendar ownership: no backfill permitted.");
        return tables;
    }
}
