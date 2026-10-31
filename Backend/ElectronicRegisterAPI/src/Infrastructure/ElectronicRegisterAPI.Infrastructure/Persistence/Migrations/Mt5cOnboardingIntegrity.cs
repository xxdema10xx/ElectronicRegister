using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Read-only classification for historical backfill tools. Never applies onboarding SQL.
internal static class Mt5cOnboardingIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection connection, MySqlTransaction? transaction)
    {
        string[] tables = ["institution_invitation", "institution_onboarding_event", "platform_operation_nonce", "institution_provisioning"];
        var scope = "'" + string.Join("','", tables) + "'";
        async Task<long> Count(string sql)
        {
            await using var cmd = new MySqlCommand(sql, connection, transaction);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        var count = await Count($"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ({scope})");
        if (count == 0)
            return[];
        if (count != 4)
            throw new InvalidOperationException("Partial MT5C inventory; no backfill permitted.");
        string[] checks = [$"SELECT ABS(12-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND EVENT_OBJECT_TABLE IN ({scope}) AND TRIGGER_NAME IN ('mt5c_invitation_insert','mt5c_invitation_update','mt5c_invitation_delete','mt5c_event_insert','mt5c_event_update','mt5c_event_delete','mt5c_nonce_insert','mt5c_nonce_update','mt5c_nonce_delete','mt5c_provisioning_insert','mt5c_provisioning_update','mt5c_provisioning_delete') AND ACTION_TIMING='BEFORE'", $"SELECT ABS(11-COUNT(*)) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ({scope}) AND UPDATE_RULE='RESTRICT' AND DELETE_RULE='RESTRICT'", "SELECT COUNT(*) FROM institution_invitation WHERE kind NOT IN ('FirstAdmin','Membership','Recovery') OR status NOT IN ('pending','consumed','revoked') OR attempts NOT BETWEEN 0 AND 5 OR (status='consumed')<>(consumed_membership_id IS NOT NULL) OR token_hash NOT REGEXP BINARY '^[A-F0-9]{64}$' OR channel_hash NOT REGEXP BINARY '^[A-F0-9]{64}$'", "SELECT COUNT(*) FROM institution_provisioning p LEFT JOIN institution_onboarding_event e ON e.institution_id=p.institution_id AND e.id=p.approval_event_id LEFT JOIN institution_invitation i ON i.institution_id=p.institution_id AND i.id=p.first_invitation_id WHERE e.id IS NULL OR e.actor_kind<>'Operator' OR e.action<>'Provision' OR i.id IS NULL OR i.kind<>'FirstAdmin'", "SELECT COUNT(*) FROM platform_operation_nonce n LEFT JOIN institution_onboarding_event e ON e.institution_id=n.institution_id AND e.id=n.event_id WHERE e.id IS NULL OR e.actor_kind<>'Operator' OR e.operator_id<>n.operator_id"];
        foreach (var sql in checks)
            if (await Count(sql) != 0)
                throw new InvalidOperationException("Invalid MT5C integrity; no backfill permitted.");
        // Verify every owner-aware FK component and every live relationship, not just table names.
        var groups = new Dictionary<(string Table, string Name, string Parent), List<(string Child, string Parent)>>();
        await using (var cmd = new MySqlCommand($"SELECT TABLE_NAME,CONSTRAINT_NAME,REFERENCED_TABLE_NAME,COLUMN_NAME,REFERENCED_COLUMN_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ({scope}) AND REFERENCED_TABLE_NAME IS NOT NULL ORDER BY TABLE_NAME,CONSTRAINT_NAME,ORDINAL_POSITION", connection, transaction))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync())
            {
                var key = (reader.GetString(0), reader.GetString(1), reader.GetString(2));
                if (!groups.TryGetValue(key, out var cols))
                    groups[key] = cols = [];
                cols.Add((reader.GetString(3), reader.GetString(4)));
            }

        var expected = new Dictionary<(string Table, string Name, string Parent), string>
        {
            [("institution_invitation", "fk_invitation_owner", "institution")] = "institution_id:id",
            [("institution_invitation", "fk_invitation_recipient", "user")] = "recipient_user_id:id",
            [("institution_invitation", "fk_invitation_membership", "institution_membership")] = "institution_id:institution_id,consumed_membership_id:id",
            [("institution_onboarding_event", "fk_onboarding_event_owner", "institution")] = "institution_id:id",
            [("institution_onboarding_event", "fk_onboarding_event_invite", "institution_invitation")] = "institution_id:institution_id,invitation_id:id",
            [("institution_onboarding_event", "fk_onboarding_event_user", "user")] = "actor_user_id:id",
            [("institution_onboarding_event", "fk_onboarding_event_actor", "institution_membership")] = "institution_id:institution_id,actor_membership_id:id,actor_user_id:user_id",
            [("platform_operation_nonce", "fk_platform_nonce_event", "institution_onboarding_event")] = "institution_id:institution_id,event_id:id",
            [("institution_provisioning", "fk_provisioning_owner", "institution")] = "institution_id:id",
            [("institution_provisioning", "fk_provisioning_approval", "institution_onboarding_event")] = "institution_id:institution_id,approval_event_id:id",
            [("institution_provisioning", "fk_provisioning_invitation", "institution_invitation")] = "institution_id:institution_id,first_invitation_id:id"
        };
        if (groups.Count != expected.Count || expected.Any(x => !groups.TryGetValue(x.Key, out var cols) || string.Join(',', cols.Select(c => c.Child + ":" + c.Parent)) != x.Value))
            throw new InvalidOperationException("MT5C composite ownership differs; no backfill permitted.");
        foreach (var(key, cols)in groups)
        {
            var join = string.Join(" AND ", cols.Select(x => $"c.`{x.Child}`=p.`{x.Parent}`"));
            var nonnull = string.Join(" AND ", cols.Select(x => $"c.`{x.Child}` IS NOT NULL"));
            if (await Count($"SELECT COUNT(*) FROM `{key.Table}` c LEFT JOIN `{key.Parent}` p ON {join} WHERE {nonnull} AND p.`{cols[0].Parent}` IS NULL") != 0)
                throw new InvalidOperationException("MT5C orphan; no backfill permitted.");
        }

        return tables;
    }
}
