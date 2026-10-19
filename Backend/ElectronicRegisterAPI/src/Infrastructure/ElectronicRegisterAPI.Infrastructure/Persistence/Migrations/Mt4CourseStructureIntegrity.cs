using MySqlConnector;
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Offline migration inventory extension; never a repository or an authorization mechanism.
internal static class Mt4CourseStructureIntegrity
{
    internal static readonly string[] Tables=["course_edition","course_edition_campus","student_enrollment"];
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection connection, MySqlTransaction? transaction)
    {
        await using var inventory=new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('course_edition','course_edition_campus','student_enrollment')",connection,transaction);
        var count=Convert.ToInt32(await inventory.ExecuteScalarAsync());
        if(count==0)return [];
        if(count!=Tables.Length)throw new InvalidOperationException("Partial MT4 inventory; stop migration.");
        string[] checks=[
            "SELECT COUNT(*) FROM course_edition ce LEFT JOIN study_path sp ON sp.id=ce.study_path_id AND sp.institution_id=ce.institution_id WHERE sp.id IS NULL",
            "SELECT COUNT(*) FROM course_edition_campus ec LEFT JOIN course_edition ce ON ce.id=ec.course_edition_id AND ce.institution_id=ec.institution_id LEFT JOIN campus c ON c.id=ec.campus_id AND c.institution_id=ec.institution_id WHERE ce.id IS NULL OR c.id IS NULL",
            "SELECT COUNT(*) FROM student_enrollment se LEFT JOIN student s ON s.id=se.student_id AND s.institution_id=se.institution_id LEFT JOIN course_edition ce ON ce.id=se.course_edition_id AND ce.institution_id=se.institution_id WHERE s.id IS NULL OR ce.id IS NULL",
            "SELECT COUNT(*) FROM class c LEFT JOIN course_edition ce ON ce.id=c.course_edition_id AND ce.institution_id=c.institution_id LEFT JOIN biennium_study_path bsp ON bsp.id=c.biennium_study_path_id AND bsp.institution_id=c.institution_id WHERE c.course_edition_id IS NOT NULL AND (ce.id IS NULL OR bsp.id IS NULL OR ce.study_path_id<>bsp.study_path_id)"
        ];
        foreach(var sql in checks)
        {
            await using var check=new MySqlCommand(sql,connection,transaction);
            if(Convert.ToInt64(await check.ExecuteScalarAsync())!=0)throw new InvalidOperationException("Invalid MT4 ownership graph; no backfill permitted.");
        }
        await using var extra=new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='course_administration_event'",connection,transaction);
        if(Convert.ToInt32(await extra.ExecuteScalarAsync())==0) return Tables;
        await using var auditCheck=new MySqlCommand("SELECT COUNT(*) FROM course_administration_event a LEFT JOIN institution_membership m ON m.institution_id=a.institution_id AND m.id=a.actor_membership_id AND m.user_id=a.actor_user_id LEFT JOIN course_edition c ON c.institution_id=a.institution_id AND c.id=a.course_edition_id LEFT JOIN student_enrollment e ON e.institution_id=a.institution_id AND e.id=a.enrollment_id AND e.course_edition_id=a.course_edition_id WHERE m.id IS NULL OR c.id IS NULL OR (a.enrollment_id IS NOT NULL AND e.id IS NULL)",connection,transaction);
        if(Convert.ToInt64(await auditCheck.ExecuteScalarAsync())!=0) throw new InvalidOperationException("Invalid MT4C audit ownership graph; no backfill permitted.");
        return [..Tables,"course_administration_event"];
    }
}
