using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Read-only classification of the additive ledger for explicit offline tools.
// Never registered at bootstrap and never used as an authorization substitute.
internal static class Mt5dMembershipEventIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection connection, MySqlTransaction? transaction)
    {
        const string table = "institution_membership_event";
        await using var inventory = new MySqlCommand("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_event'", connection, transaction);
        if (Convert.ToInt32(await inventory.ExecuteScalarAsync()) == 0)
            return[];
        string[] checks = ["SELECT COUNT(*) FROM institution_membership_event e LEFT JOIN institution_membership a ON a.institution_id=e.institution_id AND a.id=e.actor_membership_id AND a.user_id=e.actor_user_id LEFT JOIN institution_membership t ON t.institution_id=e.institution_id AND t.id=e.target_membership_id LEFT JOIN institution_membership_security_state s ON s.institution_id=t.institution_id AND s.membership_id=t.id WHERE a.id IS NULL OR t.id IS NULL OR s.membership_id IS NULL OR s.security_generation<e.revision_after OR e.actor_membership_id=e.target_membership_id OR e.revision_before<0 OR e.revision_after<=e.revision_before OR e.revision_after-e.revision_before<>1", "SELECT ABS(4-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND EVENT_OBJECT_TABLE='institution_membership_event' AND ACTION_TIMING='BEFORE' AND ((TRIGGER_NAME IN ('mt5d_event_insert','mt5d_event_snapshot_shape') AND EVENT_MANIPULATION='INSERT') OR (TRIGGER_NAME='mt5d_event_update' AND EVENT_MANIPULATION='UPDATE') OR (TRIGGER_NAME='mt5d_event_delete' AND EVENT_MANIPULATION='DELETE'))", "SELECT ABS(1-COUNT(*)) FROM information_schema.TRIGGERS a JOIN information_schema.TRIGGERS b ON a.TRIGGER_SCHEMA=b.TRIGGER_SCHEMA AND a.EVENT_OBJECT_TABLE=b.EVENT_OBJECT_TABLE AND b.TRIGGER_NAME='mt5d_event_insert' WHERE a.TRIGGER_SCHEMA=DATABASE() AND a.TRIGGER_NAME='mt5d_event_snapshot_shape' AND a.ACTION_ORDER=b.ACTION_ORDER+1", "SELECT ABS(2-COUNT(*)) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_event' AND REFERENCED_TABLE_NAME='institution_membership' AND CONSTRAINT_NAME IN ('fk_membership_event_target','fk_membership_event_actor') AND DELETE_RULE='RESTRICT' AND UPDATE_RULE='RESTRICT'", "SELECT ABS(1-COUNT(*)) FROM (SELECT CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_event' AND CONSTRAINT_NAME='fk_membership_event_actor' GROUP BY CONSTRAINT_NAME HAVING COUNT(*)=3 AND GROUP_CONCAT(CONCAT(COLUMN_NAME,':',REFERENCED_COLUMN_NAME) ORDER BY ORDINAL_POSITION)='institution_id:institution_id,actor_membership_id:id,actor_user_id:user_id') x", "SELECT ABS(1-COUNT(*)) FROM (SELECT CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_event' AND CONSTRAINT_NAME='fk_membership_event_target' GROUP BY CONSTRAINT_NAME HAVING COUNT(*)=2 AND GROUP_CONCAT(CONCAT(COLUMN_NAME,':',REFERENCED_COLUMN_NAME) ORDER BY ORDINAL_POSITION)='institution_id:institution_id,target_membership_id:id') x", "SELECT ABS(1-COUNT(*)) FROM (SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_event' AND NON_UNIQUE=0 GROUP BY INDEX_NAME HAVING COUNT(*)=2 AND SUM(SUB_PART IS NOT NULL)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='institution_id,id') x", "SELECT ABS(1-COUNT(*)) FROM (SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_event' AND NON_UNIQUE=0 GROUP BY INDEX_NAME HAVING COUNT(*)=3 AND SUM(SUB_PART IS NOT NULL)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='institution_id,target_membership_id,revision_after') x"];
        foreach (var sql in checks)
        {
            await using var check = new MySqlCommand(sql, connection, transaction);
            if (Convert.ToInt64(await check.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Invalid MT5D IAM ownership/inventory; no backfill permitted.");
        }

        return[table];
    }
}
