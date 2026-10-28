using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

internal static class InstitutionImportIntegrity
{
    internal static async Task<string[]> ValidateIfPresentAsync(MySqlConnection c, MySqlTransaction? tx)
    {
        string[] tables = ["institution_import_batch", "institution_import_item", "institution_import_event", "institution_import_external_reference"];
        async Task<long> Scalar(string sql)
        {
            await using var cmd = new MySqlCommand(sql, c, tx);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        var count = await Scalar("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('institution_import_batch','institution_import_item','institution_import_event','institution_import_external_reference')");
        if (count == 0)
            return[];
        if (count != 4)
            throw new InvalidOperationException("Incomplete import schema; no backfill permitted.");
        string[] checks = ["SELECT COUNT(*) FROM institution_import_batch WHERE revision<1 OR valid_rows+invalid_rows<>total_rows OR (state='Preview' AND payload IS NULL) OR (state<>'Preview' AND payload IS NOT NULL) OR (state='Completed' AND result_json IS NULL)", "SELECT COUNT(*) FROM institution_import_batch b WHERE b.state='Completed' AND (SELECT COUNT(*) FROM institution_import_item i WHERE i.institution_id=b.institution_id AND i.batch_id=b.id)<>b.total_rows", "SELECT COUNT(*) FROM institution_import_item i JOIN institution_import_batch b ON b.institution_id=i.institution_id AND b.id=i.batch_id WHERE b.state<>'Completed'", "SELECT COUNT(*) FROM institution_import_item WHERE NOT((category='Student' AND student_id IS NOT NULL AND teacher_id IS NULL) OR (category='Teacher' AND student_id IS NULL AND teacher_id IS NOT NULL))", "SELECT COUNT(*) FROM (SELECT b.institution_id,b.id,b.revision,COUNT(e.id) n,MIN(e.revision) lo,MAX(e.revision) hi FROM institution_import_batch b LEFT JOIN institution_import_event e ON e.institution_id=b.institution_id AND e.batch_id=b.id GROUP BY b.institution_id,b.id,b.revision HAVING n<>b.revision OR lo<>1 OR hi<>b.revision) x", "SELECT ABS(13-COUNT(*)) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ('institution_import_batch','institution_import_item','institution_import_event','institution_import_external_reference')", "SELECT ABS(12-COUNT(*)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('import_batch_insert','import_batch_update','import_batch_delete','import_external_insert','import_external_update','import_external_delete','import_item_insert','import_item_update','import_item_delete','import_event_insert','import_event_update','import_event_delete')"];
        foreach (var sql in checks)
            if (await Scalar(sql) != 0)
                throw new InvalidOperationException("Invalid import history, ownership or inventory; no backfill permitted.");
        return tables;
    }
}
