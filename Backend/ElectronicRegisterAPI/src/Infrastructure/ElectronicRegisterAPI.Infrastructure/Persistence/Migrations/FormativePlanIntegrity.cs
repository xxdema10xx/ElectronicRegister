using MySqlConnector;
namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

// Read-only classification for legacy migration preflight. Never backfills formative data.
internal static class FormativePlanIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection connection,MySqlTransaction? transaction)
    {
        string[] tables=["formative_plan","formative_plan_version","formative_unit","formative_module",
            "formative_module_competency","formative_module_content","course_edition_formative_plan","formative_plan_event"];
        var names=string.Join(',',tables.Select(t=>"'"+t+"'"));
        async Task<long> Scalar(string sql)
        {await using var command=new MySqlCommand(sql,connection,transaction);return Convert.ToInt64(await command.ExecuteScalarAsync());}
        var count=await Scalar($"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ({names})");
        if(count==0)return[];
        if(count!=tables.Length)throw new InvalidOperationException("Incomplete formative schema; no backfill permitted.");
        string[] checks=[
            $"SELECT ABS(13-COUNT(*)) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ({names})",
            $"SELECT ABS(24-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND EVENT_OBJECT_TABLE IN ({names})",
            "SELECT COUNT(*) FROM formative_plan_version v JOIN formative_plan p ON p.institution_id=v.institution_id AND p.id=v.plan_id WHERE p.study_path_id<>v.study_path_id OR v.revision<0 OR v.theory_planned_minutes NOT BETWEEN 1 AND 10000000 OR v.internship_planned_minutes NOT BETWEEN 0 AND 10000000",
            "SELECT COUNT(*) FROM formative_plan_version v WHERE status='Published' AND (published_at_utc IS NULL OR theory_planned_minutes<>(SELECT COALESCE(SUM(planned_minutes),0) FROM formative_module m WHERE m.institution_id=v.institution_id AND m.version_id=v.id))",
            "SELECT COUNT(*) FROM course_edition_formative_plan b JOIN formative_plan_version v ON v.institution_id=b.institution_id AND v.id=b.version_id JOIN course_edition e ON e.institution_id=b.institution_id AND e.id=b.course_edition_id WHERE v.status<>'Published' OR v.study_path_id<>e.study_path_id OR v.study_path_id<>b.study_path_id",
            "SELECT COUNT(*) FROM formative_plan_event e LEFT JOIN institution_membership m ON m.institution_id=e.institution_id AND m.id=e.actor_membership_id AND m.user_id=e.actor_user_id WHERE m.id IS NULL OR JSON_TYPE(e.after_json)<>'OBJECT'"
        ];
        foreach(var sql in checks)if(await Scalar(sql)!=0)throw new InvalidOperationException("Invalid formative ownership or inventory; no backfill permitted.");
        return tables;
    }
}
