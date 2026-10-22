using MySqlConnector;
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
internal static class StudentClassTransferIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection c,MySqlTransaction? tx)
    {
        await using var inventory=new MySqlCommand("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='student_class_transfer_event'",c,tx);
        if(Convert.ToInt32(await inventory.ExecuteScalarAsync())==0)return [];
        string[] checks=[
            "SELECT ABS(9-COUNT(*)) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='student_class_transfer_event' AND DELETE_RULE='RESTRICT' AND UPDATE_RULE='RESTRICT'",
            "SELECT ABS(4-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('transfer_event_insert','transfer_event_update','transfer_event_delete','student_transfer_revision_update')",
            "SELECT ABS(2-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('transfer_event_update','transfer_event_delete') AND ACTION_STATEMENT LIKE '%Transfer audit append only%' AND ACTION_STATEMENT NOT LIKE '%IF %'",
            "SELECT COUNT(*) FROM student_class_transfer_event e LEFT JOIN student s ON s.institution_id=e.institution_id AND s.id=e.student_id LEFT JOIN institution_membership m ON m.institution_id=e.institution_id AND m.id=e.actor_membership_id AND m.user_id=e.actor_user_id WHERE s.id IS NULL OR m.id IS NULL OR e.revision_after<>e.revision_before+1 OR e.revision_before<0 OR e.applied_limit_months NOT BETWEEN 1 AND 120",
            "SELECT COUNT(*) FROM (SELECT institution_id,student_id FROM student_class_transfer_event GROUP BY institution_id,student_id HAVING MIN(revision_after)<>1 OR MAX(revision_after)<>COUNT(*)) x"
        ];
        foreach(var sql in checks){await using var cmd=new MySqlCommand(sql,c,tx);if(Convert.ToInt64(await cmd.ExecuteScalarAsync())!=0)throw new InvalidOperationException("Transfer ledger integrity failed; no backfill permitted.");}
        return ["student_class_transfer_event"];
    }
}
