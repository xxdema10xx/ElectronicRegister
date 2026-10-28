using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Read-only schema classification for explicit backfill preflight, never authorization.
internal static class InstitutionEmailIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection c, MySqlTransaction? tx)
    {
        string[] tables = ["institution_email_mutex", "institution_email_policy", "institution_email_assignment", "institution_email_reservation", "institution_email_event"];
        async Task<long> Scalar(string sql)
        {
            await using var cmd = new MySqlCommand(sql, c, tx);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        var count = await Scalar("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('institution_email_mutex','institution_email_policy','institution_email_assignment','institution_email_reservation','institution_email_event')");
        if (count == 0)
            return[];
        if (count != 5)
            throw new InvalidOperationException("Incomplete institutional email schema; no backfill permitted.");
        string[] checks = ["SELECT ABS(1-COUNT(*)) FROM institution_email_mutex WHERE id=1", "SELECT COUNT(*) FROM user u LEFT JOIN institution_email_reservation r ON r.address=LOWER(u.email) AND r.user_id=u.id WHERE r.address IS NULL", "SELECT COUNT(*) FROM institution_email_assignment a LEFT JOIN institution_email_reservation r ON r.address=LOWER(a.email) AND r.institution_id=a.institution_id AND r.assignment_id=a.id WHERE r.address IS NULL", "SELECT COUNT(*) FROM institution_email_reservation WHERE BINARY address<>BINARY LOWER(address) OR NOT((user_id IS NOT NULL AND institution_id IS NULL AND assignment_id IS NULL) OR (user_id IS NULL AND institution_id IS NOT NULL AND assignment_id IS NOT NULL))", "SELECT COUNT(*) FROM institution_email_assignment a LEFT JOIN institution_email_policy p ON p.institution_id=a.institution_id AND p.category=a.category AND p.revision=a.policy_revision WHERE p.policy_id IS NULL OR a.revision<1 OR (a.student_id IS NOT NULL AND a.teacher_id IS NOT NULL) OR (a.student_id IS NULL AND a.teacher_id IS NULL AND a.membership_id IS NULL)", "SELECT COUNT(*) FROM institution_email_event e LEFT JOIN institution_membership m ON m.institution_id=e.institution_id AND m.id=e.actor_membership_id AND m.user_id=e.actor_user_id WHERE m.id IS NULL OR JSON_TYPE(e.after_json)<>'OBJECT'", "SELECT COUNT(*) FROM (SELECT institution_id,category FROM institution_email_policy GROUP BY institution_id,category HAVING MIN(revision)<>1 OR MAX(revision)<>COUNT(*) OR COUNT(DISTINCT policy_id)<>1) x", "SELECT ABS(18-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('email_reservation_insert','email_reservation_update','email_reservation_delete','email_user_insert','email_user_update','email_policy_insert','email_policy_update','email_policy_delete','email_assignment_insert','email_assignment_reserve','email_assignment_update','email_assignment_override_reserve','email_assignment_delete','email_event_insert','email_event_update','email_event_delete','email_student_profile_link','email_teacher_profile_link')", "SELECT ABS(14-COUNT(*)) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ('institution_email_policy','institution_email_assignment','institution_email_reservation','institution_email_event')"];
        foreach (var sql in checks)
            if (await Scalar(sql) != 0)
                throw new InvalidOperationException("Invalid institutional email ownership, namespace or schema inventory; no backfill permitted.");
        return tables;
    }
}
