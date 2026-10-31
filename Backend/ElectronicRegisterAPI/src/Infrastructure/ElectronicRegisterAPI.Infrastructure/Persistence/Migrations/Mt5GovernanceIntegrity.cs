using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

internal static class Mt5GovernanceIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection connection, MySqlTransaction? transaction)
    {
        const string scope = "'institution_recovery','institution_recovery_approval'";
        async Task<long> Count(string sql)
        {
            await using var cmd = new MySqlCommand(sql, connection, transaction);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        var n = await Count($"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ({scope})");
        if (n == 0)
            return[];
        if (n != 2)
            throw new InvalidOperationException("Partial governance inventory; no backfill permitted.");
        string[] checks = ["SELECT ABS(8-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('mt5gov_institution_transition','mt5gov_institution_generation','mt5gov_recovery_insert','mt5gov_recovery_update','mt5gov_recovery_delete','mt5gov_approval_insert','mt5gov_approval_update','mt5gov_approval_delete')", $"SELECT ABS(6-COUNT(*)) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ({scope}) AND UPDATE_RULE='RESTRICT' AND DELETE_RULE='RESTRICT'", "SELECT COUNT(*) FROM institution_recovery r LEFT JOIN institution_invitation i ON i.institution_id=r.institution_id AND i.id=r.id LEFT JOIN institution_onboarding_event e ON e.institution_id=r.institution_id AND e.id=r.request_event_id WHERE i.id IS NULL OR i.kind<>'Recovery' OR i.recipient_user_id<>r.recipient_user_id OR e.id IS NULL OR e.actor_kind<>'Operator' OR e.action<>'RecoveryRequested' OR JSON_TYPE(r.signed_request)<>'OBJECT'", "SELECT COUNT(*) FROM institution_recovery_approval a LEFT JOIN institution_onboarding_event e ON e.institution_id=a.institution_id AND e.id=a.event_id WHERE e.id IS NULL OR e.actor_kind<>'Operator' OR e.operator_id<>a.operator_id OR e.action<>'RecoveryApproved' OR JSON_TYPE(a.signed_operation)<>'OBJECT'", "SELECT COUNT(*) FROM (SELECT institution_id,recovery_id FROM institution_recovery_approval GROUP BY institution_id,recovery_id HAVING COUNT(*)>2) a"];
        foreach (var sql in checks)
            if (await Count(sql) != 0)
                throw new InvalidOperationException("Invalid governance graph; no backfill permitted.");
        var expected = new Dictionary<(string Table, string Key, string Parent), string>
        {
            [("institution_recovery", "fk_recovery_owner", "institution")] = "institution_id:id",
            [("institution_recovery", "fk_recovery_invitation", "institution_invitation")] = "institution_id:institution_id,id:id",
            [("institution_recovery", "fk_recovery_recipient", "user")] = "recipient_user_id:id",
            [("institution_recovery", "fk_recovery_request", "institution_onboarding_event")] = "institution_id:institution_id,request_event_id:id",
            [("institution_recovery_approval", "fk_recovery_approval_case", "institution_recovery")] = "institution_id:institution_id,recovery_id:id",
            [("institution_recovery_approval", "fk_recovery_approval_event", "institution_onboarding_event")] = "institution_id:institution_id,event_id:id"
        };
        var actual = new Dictionary<(string Table, string Key, string Parent), List<(string Child, string Parent)>>();
        await using (var cmd = new MySqlCommand($"SELECT TABLE_NAME,CONSTRAINT_NAME,REFERENCED_TABLE_NAME,COLUMN_NAME,REFERENCED_COLUMN_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ({scope}) AND REFERENCED_TABLE_NAME IS NOT NULL ORDER BY TABLE_NAME,CONSTRAINT_NAME,ORDINAL_POSITION", connection, transaction))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync())
            {
                var key = (reader.GetString(0), reader.GetString(1), reader.GetString(2));
                if (!actual.TryGetValue(key, out var cols))
                    actual[key] = cols = [];
                cols.Add((reader.GetString(3), reader.GetString(4)));
            }

        if (actual.Count != expected.Count || expected.Any(x => !actual.TryGetValue(x.Key, out var c) || string.Join(',', c.Select(y => y.Child + ":" + y.Parent)) != x.Value))
            throw new InvalidOperationException("Governance composite ownership differs.");
        foreach (var(key, cols)in actual)
        {
            var on = string.Join(" AND ", cols.Select(x => $"c.`{x.Child}`=p.`{x.Parent}`"));
            var nonnull = string.Join(" AND ", cols.Select(x => $"c.`{x.Child}` IS NOT NULL"));
            if (await Count($"SELECT COUNT(*) FROM `{key.Table}` c LEFT JOIN `{key.Parent}` p ON {on} WHERE {nonnull} AND p.`{cols[0].Parent}` IS NULL") != 0)
                throw new InvalidOperationException("Governance orphan; no backfill permitted.");
        }

        return["institution_recovery", "institution_recovery_approval"];
    }
}
