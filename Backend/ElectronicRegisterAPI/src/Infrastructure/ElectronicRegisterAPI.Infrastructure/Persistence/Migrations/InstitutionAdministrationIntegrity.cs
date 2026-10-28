using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Read-only classification for explicit offline backfill preflight. Not an authorization path.
internal static class InstitutionAdministrationIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection connection, MySqlTransaction? transaction)
    {
        const string table = "institution_administration_event";
        await using var inventory = new MySqlCommand("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_administration_event'", connection, transaction);
        if (Convert.ToInt32(await inventory.ExecuteScalarAsync()) == 0)
            return[];
        string[] checks = ["SELECT COUNT(*) FROM institution_administration_event e LEFT JOIN institution_membership m ON m.institution_id=e.institution_id AND m.id=e.actor_membership_id AND m.user_id=e.actor_user_id LEFT JOIN institution i ON i.id=e.institution_id WHERE m.id IS NULL OR i.id IS NULL OR e.revision<1 OR e.action NOT IN ('created','updated','deleted') OR (e.before_json IS NOT NULL AND NOT JSON_VALID(e.before_json)) OR (e.after_json IS NOT NULL AND NOT JSON_VALID(e.after_json)) OR (e.action='created' AND (e.before_json IS NOT NULL OR e.after_json IS NULL OR e.revision<>1)) OR (e.action='updated' AND (e.before_json IS NULL OR e.after_json IS NULL)) OR (e.action='deleted' AND (e.before_json IS NULL OR e.after_json IS NOT NULL))", "SELECT COUNT(*) FROM (SELECT institution_id,entity_type,entity_id FROM institution_administration_event GROUP BY institution_id,entity_type,entity_id HAVING MIN(revision)<>1 OR MAX(revision)<>COUNT(*)) x", "SELECT ABS(3-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND EVENT_OBJECT_TABLE='institution_administration_event' AND ACTION_TIMING='BEFORE' AND ((TRIGGER_NAME='administration_event_insert' AND EVENT_MANIPULATION='INSERT') OR (TRIGGER_NAME='administration_event_update' AND EVENT_MANIPULATION='UPDATE') OR (TRIGGER_NAME='administration_event_delete' AND EVENT_MANIPULATION='DELETE'))", "SELECT ABS(2-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('administration_event_update','administration_event_delete') AND ACTION_STATEMENT LIKE '%Administration audit append only%' AND ACTION_STATEMENT NOT LIKE '%IF %'", "SELECT ABS(2-COUNT(*)) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='institution_administration_event' AND CONSTRAINT_NAME IN ('fk_administration_event_owner','fk_administration_event_actor') AND DELETE_RULE='RESTRICT' AND UPDATE_RULE='RESTRICT'", "SELECT ABS(1-COUNT(*)) FROM (SELECT CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='institution_administration_event' AND CONSTRAINT_NAME='fk_administration_event_actor' GROUP BY CONSTRAINT_NAME HAVING COUNT(*)=3 AND GROUP_CONCAT(CONCAT(COLUMN_NAME,':',REFERENCED_COLUMN_NAME) ORDER BY ORDINAL_POSITION)='institution_id:institution_id,actor_membership_id:id,actor_user_id:user_id') x", "SELECT ABS(1-COUNT(*)) FROM (SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_administration_event' AND NON_UNIQUE=0 GROUP BY INDEX_NAME HAVING COUNT(*)=4 AND SUM(SUB_PART IS NOT NULL)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='institution_id,entity_type,entity_id,revision') x", "SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='institution_administration_event' AND REFERENCED_TABLE_NAME='class'"];
        foreach (var sql in checks)
        {
            await using var check = new MySqlCommand(sql, connection, transaction);
            if (Convert.ToInt64(await check.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Invalid administration ledger ownership/inventory; no backfill permitted.");
        }

        return[table];
    }
}
