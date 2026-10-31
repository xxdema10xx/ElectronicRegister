using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

// Runtime checks use metadata visible with schema-wide CRUD. Trigger definitions are
// attested separately by release preflight (review identity) and the migration validator.
internal static class ClassDeletionIntegrity
{
    internal static async Task<bool> ValidateCalendarAsync(MySqlConnection c, MySqlTransaction tx, CancellationToken ct)
    {
        async Task<long> Scalar(string sql)
        {
            await using var command = new MySqlCommand(sql, c, tx);
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        }

        // Without schema-wide SELECT, an empty metadata result cannot prove absence.
        const string grantee = "CONCAT(CHAR(39),SUBSTRING_INDEX(CURRENT_USER(),'@',1),CHAR(39),'@',CHAR(39),SUBSTRING_INDEX(CURRENT_USER(),'@',-1),CHAR(39))";
        if (await Scalar($"SELECT (SELECT COUNT(*) FROM information_schema.SCHEMA_PRIVILEGES WHERE GRANTEE={grantee} AND TABLE_SCHEMA=DATABASE() AND PRIVILEGE_TYPE='SELECT') + (SELECT COUNT(*) FROM information_schema.USER_PRIVILEGES WHERE GRANTEE={grantee} AND PRIVILEGE_TYPE='SELECT')") == 0)
            throw new InvalidOperationException("Class dependency metadata requires schema-wide SELECT visibility.");

        const string names = "'institution_calendar_settings','institution_calendar_period','institution_lesson','institution_calendar_event','institution_calendar_import','institution_calendar_occupancy'";
        var count = await Scalar($"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE' AND TABLE_NAME IN({names})");
        if (count == 0) return false; // Legacy installations without the additive calendar.
        if (count != 6)
            throw new InvalidOperationException("Incomplete calendar tables: class deletion refused.");
        if (await Scalar($"SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN({names})") != 22)
            throw new InvalidOperationException("Incomplete calendar foreign keys: class deletion refused.");
        if (await Scalar("SELECT COUNT(*) FROM institution_lesson l LEFT JOIN formative_module m ON m.institution_id=l.institution_id AND m.version_id=l.version_id AND m.id=l.module_id WHERE m.id IS NULL") != 0)
            throw new InvalidOperationException("Invalid calendar ownership: class deletion refused.");
        return true;
    }
}
