using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Offline inventory extension only. Never registered in Program or used to authorize callers.
internal static class Mt5bSecurityStateIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection c, MySqlTransaction? tx)
    {
        const string table = "institution_membership_security_state";
        await using var inventory = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='institution_membership_security_state'", c, tx);
        if (Convert.ToInt32(await inventory.ExecuteScalarAsync()) == 0)
            return[];
        string[] checks = ["SELECT COUNT(*) FROM institution_membership m LEFT JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id WHERE s.membership_id IS NULL", "SELECT COUNT(*) FROM institution_membership_security_state s LEFT JOIN institution_membership m ON m.institution_id=s.institution_id AND m.id=s.membership_id WHERE m.id IS NULL OR s.security_generation<0", "SELECT 11-COUNT(*) FROM information_schema.triggers WHERE trigger_schema=DATABASE() AND trigger_name IN ('mt5b_security_insert','mt5b_security_update','mt5b_security_delete','mt5b_membership_security_insert','mt5b_membership_identity_update','mt5b_membership_security_update','mt5b_membership_security_delete','mt5b_role_security_insert','mt5b_role_security_delete','mt5b_role_identity_update','mt5b_membership_delete_contract')", "SELECT 1-COUNT(*) FROM information_schema.referential_constraints WHERE constraint_schema=DATABASE() AND constraint_name='fk_membership_security_owner' AND table_name='institution_membership_security_state' AND referenced_table_name='institution_membership' AND delete_rule='RESTRICT' AND update_rule='RESTRICT'"];
        foreach (var sql in checks)
        {
            await using var check = new MySqlCommand(sql, c, tx);
            if (Convert.ToInt64(await check.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Invalid MT5B security ownership/inventory; no backfill permitted.");
        }

        return[table];
    }
}
