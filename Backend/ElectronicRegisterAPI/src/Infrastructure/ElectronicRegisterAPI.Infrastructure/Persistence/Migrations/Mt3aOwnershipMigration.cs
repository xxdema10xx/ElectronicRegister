using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

/// <summary>Explicit test-only migration tooling. Never registered in DI or invoked at bootstrap.</summary>
public static class Mt3aOwnershipMigration
{
    public static readonly string[] OwnedTables = ["biennium", "study_area", "study_path", "subject", "teacher", "student",
        "biennium_study_area", "biennium_study_path", "class", "class_subject", "exam", "exam_student", "grade",
        "exam_submission", "question", "question_option", "submission_answer", "submission_answer_option", "exam_violation"];
    private static readonly HashSet<string> AddedColumns = ["institution_id", "class_institution_id", "retake_institution_id", "grade_institution_id"];
    public sealed record TableDigest(string[] Columns, long Count, string Sha256);

    public static async Task<MySqlConnection> OpenAsync(string connectionString)
    {
        var b = new MySqlConnectionStringBuilder(connectionString);
        if (b.Database != "electronic_register_test" || b.Server is not ("127.0.0.1" or "localhost" or "::1"))
            throw new ArgumentException("Only an explicit loopback electronic_register_test connection is authorized.");
        var c = new MySqlConnection(connectionString);
        try
        {
            await c.OpenAsync();
            await TestDatabaseSession.ConfigureAsync(c);
            if ((string?)await new MySqlCommand("SELECT DATABASE()", c).ExecuteScalarAsync() != "electronic_register_test")
                throw new InvalidOperationException("Wrong selected database.");
            var mode = (string)(await new MySqlCommand("SELECT @@SESSION.sql_mode", c).ExecuteScalarAsync())!;
            if (!mode.Contains("STRICT_TRANS_TABLES", StringComparison.Ordinal) && !mode.Contains("STRICT_ALL_TABLES", StringComparison.Ordinal))
                throw new InvalidOperationException("Strict SQL mode required.");
            return c;
        }
        catch { await c.DisposeAsync(); throw; }
    }

    // Every legacy column, including PK/FK, grading, timestamps, hashes and audit JSON.
    // Only count/digest/column names leave memory; no cell value is written to evidence.
    public static async Task<SortedDictionary<string, TableDigest>> SnapshotAsync(MySqlConnection c, MySqlTransaction? tx = null)
    {
        var result = new SortedDictionary<string, TableDigest>(StringComparer.Ordinal);
        foreach (var table in OwnedTables.Concat(["audit_event", "user"]))
        {
            var columns = new List<string>();
            await using (var cmd = new MySqlCommand("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t ORDER BY ORDINAL_POSITION", c, tx))
            {
                cmd.Parameters.AddWithValue("@t", table);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var name = r.GetString(0);
                    if (AddedColumns.Contains(name) || (table == "submission_answer" && name == "exam_id") ||
                        (table == "submission_answer_option" && name == "question_id")) continue;
                    columns.Add(name);
                }
            }
            if (columns.Count == 0) throw new InvalidOperationException("Missing legacy table " + table);
            var rows = new List<string>();
            await using (var cmd = new MySqlCommand($"SELECT {string.Join(',', columns.Select(x => '`' + x + '`'))} FROM `{table}`", c, tx))
            {
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var cells = new List<string?>();
                    for (var i = 0; i < r.FieldCount; i++) cells.Add(r.IsDBNull(i) ? null : r.GetValue(i) switch
                    {
                        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
                        byte[] bytes => Convert.ToHexString(bytes),
                        IFormattable v => v.ToString(null, CultureInfo.InvariantCulture),
                        var v => v.ToString()
                    });
                    rows.Add(JsonSerializer.Serialize(cells));
                }
            }
            rows.Sort(StringComparer.Ordinal);
            result[table] = new(columns.ToArray(), rows.Count, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows)))));
        }
        return result;
    }

    private static async Task<string> SqlAsync(string name)
    {
        await using var s = typeof(Mt3aOwnershipMigration).Assembly.GetManifestResourceStream("MT3." + name)
            ?? throw new InvalidOperationException("Canonical MT3A SQL resource missing: " + name);
        return await new StreamReader(s).ReadToEndAsync();
    }

    public static async Task ValidateGraphAsync(MySqlConnection c, Guid legacy, MySqlTransaction? tx = null)
    {
        if (legacy == Guid.Empty) throw new ArgumentException("Explicit legacy InstitutionId required.");
        var known = OwnedTables.Concat(["audit_event", "user", "institution", "campus", "institution_membership", "institution_membership_role", "institution_student_profile", "institution_teacher_profile", "mt3a_legacy_scope"]).ToHashSet(StringComparer.Ordinal);
        known.UnionWith(await Mt4CourseStructureIntegrity.ValidateIfPresentAsync(c, tx));
        known.UnionWith(await Mt5bSecurityStateIntegrity.ValidateIfPresentAsync(c, tx));
        known.UnionWith(await Mt5dMembershipEventIntegrity.ValidateIfPresentAsync(c, tx));
        known.UnionWith(await Mt5cOnboardingIntegrity.ValidateIfPresentAsync(c, tx));
        known.UnionWith(await Mt5GovernanceIntegrity.ValidateIfPresentAsync(c, tx));
        known.UnionWith(await InstitutionClassIntegrity.ValidateIfPresentAsync(c, tx));
        known.UnionWith(await InstitutionAdministrationIntegrity.ValidateIfPresentAsync(c, tx));
        known.UnionWith(await StudentClassTransferIntegrity.ValidateIfPresentAsync(c, tx));
        await using (var tables = new MySqlCommand("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()",c,tx))
        {
            await using var reader=await tables.ExecuteReaderAsync();
            while(await reader.ReadAsync()) if(!known.Contains(reader.GetString(0))) throw new InvalidOperationException("Unclassified table: "+reader.GetString(0));
        }
        foreach (var sql in (await SqlAsync("004_ownership_preflight.sql")).Split("-- MT3A-BATCH", StringSplitOptions.RemoveEmptyEntries))
        {
            await using var cmd = new MySqlCommand(sql, c, tx);
            cmd.Parameters.AddWithValue("@legacy_id", legacy);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                if (r.GetInt64(1) != 0) throw new InvalidOperationException($"Ambiguous graph: {r.GetString(0)}, violations={r.GetInt64(1)}. No silent reassignment.");
        }
    }

    public static async Task BackfillAsync(string connectionString, Guid legacy)
    {
        await using var c = await OpenAsync(connectionString);
        await VerifyGuidTypesAsync(c);
        await BackfillConnectionAsync(c, legacy);
    }

    public static async Task BackfillConnectionAsync(MySqlConnection c, Guid legacy)
    {
        if (c.Database != "electronic_register_test" || (string?)await new MySqlCommand("SELECT DATABASE()",c).ExecuteScalarAsync() != "electronic_register_test")
            throw new ArgumentException("Only the authorized test database may be backfilled.");
        await using var tx = await c.BeginTransactionAsync();
        await using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM institution WHERE id=@id", c, tx))
        {
            cmd.Parameters.AddWithValue("@id", legacy);
            if (legacy == Guid.Empty || Convert.ToInt64(await cmd.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Explicit legacy institution is absent.");
        }
        // Caller must quiesce writers; transactional DML is not a replacement for maintenance.
        await ValidateGraphAsync(c, legacy, tx);
        var before = JsonSerializer.Serialize(await SnapshotAsync(c, tx));
        await ExecuteResourceAsync(c, "005_ownership_backfill.sql", legacy, tx);
        await ValidateGraphAsync(c, legacy, tx);
        if (before != JsonSerializer.Serialize(await SnapshotAsync(c, tx))) throw new InvalidOperationException("Legacy cell digest changed; backfill rolled back.");
        await tx.CommitAsync();
    }

    // DDL is deliberately separate from transactional backfill. Guarded PowerShell
    // wrapper exports and verifies a fresh backup before invoking either DDL stage.
    public static async Task ExecuteDdlAsync(string connectionString, string stage, Guid legacy, string verifiedBackup)
    {
        if (stage is not ("expand" or "enforce" or "audit-platform" or "prune-indexes" or "prune-fk-indexes")) throw new ArgumentException("Unknown DDL stage.");
        if (legacy == Guid.Empty) throw new ArgumentException("Explicit legacy manifest required.");
        var backup = await File.ReadAllTextAsync(verifiedBackup);
        if (new FileInfo(verifiedBackup).Length < 1000 || !backup.Contains("CREATE TABLE") || !backup.Contains("Dump completed"))
            throw new InvalidOperationException("Readable, complete backup required before DDL.");
        await using var c = await OpenAsync(connectionString);
        await VerifyGuidTypesAsync(c);
        if (stage == "prune-fk-indexes")
        {
            var count = Convert.ToInt64(await new MySqlCommand("SELECT COUNT(DISTINCT INDEX_NAME) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND INDEX_NAME IN ('idx_mt3a_exam_submission_exam','idx_mt3a_question_exam','idx_mt3a_question_option_question')",c).ExecuteScalarAsync());
            if (count != 3) throw new InvalidOperationException("Covered FK index cleanup already/partially applied; inspect before retry.");
            await ExecuteResourceAsync(c,"011_prune_covered_fk_indexes.sql",legacy,backupPath:verifiedBackup);
            return;
        }
        if (stage == "prune-indexes")
        {
            var count = Convert.ToInt64(await new MySqlCommand("SELECT COUNT(DISTINCT INDEX_NAME) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND INDEX_NAME IN ('uq_mt3a_question_option_owner','uq_mt3a_submission_answer_owner','uq_mt3a_exam_violation_owner')",c).ExecuteScalarAsync());
            if (count != 3) throw new InvalidOperationException("Index cleanup already/partially applied; inspect before retry.");
            await ExecuteResourceAsync(c,"010_prune_redundant_owner_indexes.sql",legacy,backupPath:verifiedBackup);
            return;
        }
        if (stage == "audit-platform")
        {
            var count = Convert.ToInt64(await new MySqlCommand("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('mt3a_audit_insert','mt3a_audit_update') AND ACTION_STATEMENT NOT LIKE '%MT3A platform shape required%'",c).ExecuteScalarAsync());
            if (count != 2) throw new InvalidOperationException("Audit extension already/partially applied. Inspect before retry.");
            var before = JsonSerializer.Serialize(await SnapshotAsync(c));
            await ExecuteResourceAsync(c,"009_explicit_platform_audit.sql",legacy,backupPath:verifiedBackup);
            if (before != JsonSerializer.Serialize(await SnapshotAsync(c))) throw new InvalidOperationException("Audit cells changed during trigger extension.");
            return;
        }
        await using (var cmd = new MySqlCommand(stage == "expand"
            ? "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND COLUMN_NAME='institution_id' AND TABLE_NAME IN ('" + string.Join("','", OwnedTables.Concat(["audit_event"])) + "')"
            : "SELECT (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME LIKE 'fk_mt3a_%') + (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('" + string.Join("','",OwnedTables) + "') AND COLUMN_NAME='institution_id' AND IS_NULLABLE='NO') + (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND INDEX_NAME LIKE 'uq_mt3a_%')", c))
            if (Convert.ToInt64(await cmd.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("DDL stage already/partially applied. Inspect schema; never blindly retry or DROP.");
        if (stage == "expand") await ExecuteResourceAsync(c, "003_ownership_expand.sql", legacy, backupPath: verifiedBackup);
        else
        {
            await ValidateGraphAsync(c, legacy);
            foreach (var table in OwnedTables)
                if (Convert.ToInt64(await new MySqlCommand($"SELECT COUNT(*) FROM `{table}` WHERE institution_id IS NULL", c).ExecuteScalarAsync()) != 0)
                    throw new InvalidOperationException("Backfill required before enforcing " + table);
            var before = JsonSerializer.Serialize(await SnapshotAsync(c));
            await ExecuteResourceAsync(c, "006_ownership_enforce.sql", legacy, backupPath: verifiedBackup);
            await ExecuteResourceAsync(c, "007_ownership_triggers.sql", legacy, backupPath: verifiedBackup);
            await using var install = new MySqlCommand("INSERT INTO mt3a_legacy_scope(scope,institution_id) VALUES ('legacy',@id)", c);
            install.Parameters.AddWithValue("@id", legacy);
            await install.ExecuteNonQueryAsync();
            if (before != JsonSerializer.Serialize(await SnapshotAsync(c))) throw new InvalidOperationException("Legacy digest changed during DDL. Stop; DDL has implicit commits.");
        }
    }

    private static async Task ExecuteResourceAsync(MySqlConnection c, string name, Guid legacy, MySqlTransaction? tx = null, string? backupPath = null)
    {
        foreach (var sql in (await SqlAsync(name)).Split("-- MT3A-BATCH", StringSplitOptions.RemoveEmptyEntries))
        {
            if (backupPath != null) await BackupBeforeDdlAsync(c, backupPath);
            await using var cmd = new MySqlCommand(sql, c, tx) { CommandTimeout = 120 };
            cmd.Parameters.AddWithValue("@legacy_id", legacy);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyGuidTypesAsync(MySqlConnection c)
    {
        // Operator/key identifiers are explicit authority names, not Guid resource keys.
        // Only their exact owner tables, names, types and collation are classified.
        var wrong = Convert.ToInt64(await new MySqlCommand("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND (COLUMN_NAME='id' OR RIGHT(COLUMN_NAME,3)='_id') AND COLUMN_NAME<>'request_id' AND NOT(((TABLE_NAME IN ('institution_onboarding_event','platform_operation_nonce') AND COLUMN_NAME IN ('operator_id','key_id')) OR (TABLE_NAME='institution_recovery_approval' AND COLUMN_NAME='operator_id')) AND COLUMN_TYPE='varchar(64)' AND COLLATION_NAME='ascii_bin') AND (COLUMN_TYPE<>'char(36)' OR COLLATION_NAME<>'ascii_general_ci')",c).ExecuteScalarAsync());
        if (wrong != 0) throw new InvalidOperationException("Guid types/collations differ; review before migration.");
    }

    public static async Task BackupBeforeDdlAsync(MySqlConnection c, string basePath)
    {
        // Each CREATE/ALTER (including triggers) has its own verified backup and
        // target/version/mode evidence. Never attempt a rollback with destructive DROP.
        var b = new MySqlConnectionStringBuilder(c.ConnectionString);
        var path = Path.Combine(Path.GetDirectoryName(basePath)!, "ddl-" + Guid.NewGuid().ToString("N") + ".sql");
        await using var check = new MySqlCommand("SELECT DATABASE(),VERSION(),@@SESSION.sql_mode", c);
        await using var r = await check.ExecuteReaderAsync();
        await r.ReadAsync();
        var database = r.GetString(0); var version = r.GetString(1); var mode = r.GetString(2);
        await r.DisposeAsync();
        if (database != "electronic_register_test" || !mode.Contains("STRICT_", StringComparison.Ordinal)) throw new InvalidOperationException("DDL target/mode changed.");
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("MYSQLDUMP_EXE") ?? "C:/Program Files/MySQL/MySQL Server 8.0/bin/mysqldump.exe")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in new[] { "--host=" + b.Server, "--port=" + b.Port, "--user=" + b.UserID, "--single-transaction", "--skip-add-drop-table", "--result-file=" + path }) start.ArgumentList.Add(arg);
        // XAMPP's MariaDB client does not support the MySQL 8 dump flags. Explicit
        // operator opt-in only; backup target, completeness and digest checks remain identical.
        var dumpFlavor = Environment.GetEnvironmentVariable("MYSQLDUMP_FLAVOR");
        if (string.Equals(dumpFlavor, "MySQL57", StringComparison.Ordinal))
            start.ArgumentList.Add("--set-gtid-purged=OFF");
        else if (!string.Equals(dumpFlavor, "MariaDB", StringComparison.Ordinal))
            foreach (var arg in new[] { "--column-statistics=0", "--no-tablespaces", "--set-gtid-purged=OFF" }) start.ArgumentList.Add(arg);
        start.ArgumentList.Add(database);
        // The open connector hides passwords by default. Only the original env
        // connection can supply the dump credential; never log it or use argv.
        var original = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION") ?? throw new InvalidOperationException("Explicit test connection required for backups."));
        if (original.Database != database || original.Server != b.Server || original.UserID != b.UserID || original.Port != b.Port) throw new InvalidOperationException("Backup connection differs.");
        start.Environment["MYSQL_PWD"] = original.Password;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start backup.");
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) { await stderr; throw new InvalidOperationException("Pre-DDL backup failed. No DDL executed for this batch."); }
        var bytes = await File.ReadAllBytesAsync(path);
        var content = Encoding.UTF8.GetString(bytes);
        if (bytes.Length < 1000 || !content.Contains("CREATE TABLE") || !content.Contains("Dump completed")) throw new InvalidOperationException("Incomplete pre-DDL backup.");
        await using(var tables=new MySqlCommand("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE'",c))
        {
            await using var reader=await tables.ExecuteReaderAsync();
            while(await reader.ReadAsync()) if(!content.Contains("CREATE TABLE `"+reader.GetString(0)+"`")) throw new InvalidOperationException("Backup misses a table definition.");
        }
        var evidence = new { database, version, mode, backup = Path.GetFileName(path), sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        await File.AppendAllTextAsync(Path.Combine(Path.GetDirectoryName(basePath)!, "ddl-backups.jsonl"), JsonSerializer.Serialize(evidence) + Environment.NewLine);
    }
}
