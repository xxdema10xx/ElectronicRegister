using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Global identity tables have no tenant owner. Legacy backfill must never rewrite them.
internal static class IdentityAccountIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection connection, MySqlTransaction? transaction)
    {
        async Task<long> Scalar(string sql)
        {
            await using var command = new MySqlCommand(sql, connection, transaction);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        const string tables = "'identity_account_state','identity_account_event'";
        var count = await Scalar($"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ({tables})");
        if (count == 0)
            return[];
        if (count != 2 || await Scalar($"SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ({tables}) AND REFERENCED_TABLE_NAME='user' AND DELETE_RULE='RESTRICT'") != 2 || await Scalar("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('identity_account_event_no_update','identity_account_event_no_delete','identity_account_state_guard')") != 3)
            throw new InvalidOperationException("Incomplete identity account schema; no backfill permitted.");
        if (await Scalar("SELECT COUNT(*) FROM identity_account_state s LEFT JOIN `user` u ON u.id=s.user_id WHERE u.id IS NULL OR s.generation<0") != 0 || await Scalar("SELECT COUNT(*) FROM identity_account_event e LEFT JOIN `user` u ON u.id=e.user_id WHERE u.id IS NULL OR e.action NOT IN ('account.registered','password.changed','profile.updated')") != 0)
            throw new InvalidOperationException("Invalid global identity state; no backfill permitted.");
        var known = new List<string>
        {
            "identity_account_state",
            "identity_account_event"
        };
        // Optional additive release/operational state, never tenant-owned data.
        foreach (var table in new[] { "identity_password_attempt", "er_schema_release" })
            if (await Scalar($"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table}'") == 1)
                known.Add(table);
        if (await Scalar("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_profile_acquisition'") == 1)
        {
            if (await Scalar("SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='institution_profile_acquisition' AND DELETE_RULE='RESTRICT'") != 4 || await Scalar("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND EVENT_OBJECT_TABLE='institution_profile_acquisition'") != 3 || await Scalar("SELECT COUNT(*) FROM institution_profile_acquisition WHERE (student_id IS NULL)=(teacher_id IS NULL) OR status NOT IN ('linked','requires-review') OR request_hash NOT REGEXP '^[A-F0-9]{64}$'") != 0)
                throw new InvalidOperationException("Invalid profile acquisition ledger; no backfill permitted.");
            known.Add("institution_profile_acquisition");
        }

        return known.ToArray();
    }
}
