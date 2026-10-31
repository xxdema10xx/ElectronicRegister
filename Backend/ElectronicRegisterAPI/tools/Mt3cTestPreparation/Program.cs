// Explicit recovery of an EF EnsureCreated TEST database missing SQL-only MT3A artifacts.
// Never registered by the API. Refuses deployed/partially migrated databases.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using MySqlConnector;

var connection = Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION") ?? throw new ArgumentException("TEST_MYSQL_CONNECTION required");
if (args.Length != 1 || args[0] is not ("prepare-ef-created-test-only" or "resume-reviewed-columns-only")) throw new ArgumentException("Explicit test preparation required");
var resume = args[0] == "resume-reviewed-columns-only";
await using var db = await Mt3aOwnershipMigration.OpenAsync(connection);
var output = Path.GetFullPath("mt3c-evidence/private");
Directory.CreateDirectory(output);
var legacy = Guid.Parse("4d543341-0000-4000-8000-000000000001");
async Task<long> Count(string sql) => Convert.ToInt64(await new MySqlCommand(sql, db).ExecuteScalarAsync());
if (await Count("SELECT COUNT(*) FROM institution") != (resume ? 1 : 0) || await Count("SELECT COUNT(*) FROM institution_membership") != 0 ||
    await Count("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()") != 0 ||
    await Count("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME LIKE 'fk_mt3a_%'") != 0 ||
    await Count("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND COLUMN_NAME IN ('class_institution_id','retake_institution_id','grade_institution_id')") != (resume ? 3 : 0))
    throw new InvalidOperationException("Not a pristine EF-created test schema. Inspect manually; no automatic retry.");
foreach (var table in Mt3aOwnershipMigration.OwnedTables)
    if (await Count($"SELECT COUNT(*) FROM `{table}` WHERE institution_id <> '00000000-0000-0000-0000-000000000000'") != 0)
        throw new InvalidOperationException("Unexpected assigned owner: " + table);
var before = JsonSerializer.Serialize(await Mt3aOwnershipMigration.SnapshotAsync(db));
async Task Backup()
{
    var b = new MySqlConnectionStringBuilder(connection);
    var path = Path.Combine(output, Guid.NewGuid().ToString("N") + ".sql");
    var start = new ProcessStartInfo("C:/xampp/mysql/bin/mysqldump.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
    foreach (var arg in new[] { "--host=" + b.Server, "--port=" + b.Port, "--user=" + b.UserID, "--single-transaction", "--skip-add-drop-table", "--result-file=" + path, "electronic_register_test" }) start.ArgumentList.Add(arg);
    start.Environment["MYSQL_PWD"] = b.Password;
    using var process = Process.Start(start)!;
    var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) { await error; throw new InvalidOperationException("Backup failed"); }
    var bytes = await File.ReadAllBytesAsync(path);
    var text = System.Text.Encoding.UTF8.GetString(bytes);
    if (!text.Contains("CREATE TABLE") || !text.Contains("Dump completed")) throw new InvalidOperationException("Incomplete backup");
    await File.AppendAllTextAsync(Path.Combine(output, "backups.jsonl"), JsonSerializer.Serialize(new { file = Path.GetFileName(path), sha256 = Convert.ToHexString(SHA256.HashData(bytes)) }) + Environment.NewLine);
}
async Task Execute(string sql)
{
    await using var command = new MySqlCommand(sql, db) { CommandTimeout = 120 };
    command.Parameters.AddWithValue("@legacy_id", legacy);
    await command.ExecuteNonQueryAsync();
}
async Task Resource(string name, bool ddl)
{
    await using var stream = typeof(Mt3aOwnershipMigration).Assembly.GetManifestResourceStream("MT3." + name)!;
    foreach (var batch in (await new StreamReader(stream).ReadToEndAsync()).Split("-- MT3A-BATCH", StringSplitOptions.RemoveEmptyEntries))
    { if (ddl) await Backup(); await Execute(batch); }
}
await Backup();
// Only the five SQL-only columns are absent from this EF-created shape.
foreach (var (table, column) in (resume ? Array.Empty<(string, string)>() : new[] { ("student", "class_institution_id"), ("exam", "retake_institution_id"), ("exam_submission", "grade_institution_id"), ("submission_answer", "exam_id"), ("submission_answer_option", "question_id") }))
{
    await Backup();
    await Execute($"ALTER TABLE `{table}` ADD COLUMN `{column}` CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL");
}
if (!resume) await Execute("INSERT INTO institution(id,name,code,status) VALUES (@legacy_id,'MT3A test migration holding - not a verified legal institution','MT3A_LEGACY_TEST','provisioning')");
else if (await Count("SELECT COUNT(*) FROM institution WHERE id='4d543341-0000-4000-8000-000000000001' AND code='MT3A_LEGACY_TEST' AND status='provisioning'") != 1) throw new InvalidOperationException("Unexpected holding");
await Execute("START TRANSACTION");
try
{
    foreach (var table in Mt3aOwnershipMigration.OwnedTables)
        await Execute($"UPDATE `{table}` SET institution_id=@legacy_id" + (table is "exam" or "exam_submission" ? ",updated_at=updated_at" : "") + " WHERE institution_id='00000000-0000-0000-0000-000000000000'");
    await Resource("005_ownership_backfill.sql", false);
    await Mt3aOwnershipMigration.ValidateGraphAsync(db, legacy);
    if (before != JsonSerializer.Serialize(await Mt3aOwnershipMigration.SnapshotAsync(db))) throw new InvalidOperationException("Legacy data changed");
    await Execute("COMMIT");
}
catch { await Execute("ROLLBACK"); throw; }
await Resource("006_ownership_enforce.sql", true);
await Resource("007_ownership_triggers.sql", true);
await Execute("INSERT INTO mt3a_legacy_scope(scope,institution_id) VALUES ('legacy',@legacy_id)");
await Resource("009_explicit_platform_audit.sql", true);
await Resource("010_prune_redundant_owner_indexes.sql", true);
await Resource("011_prune_covered_fk_indexes.sql", true);
await Mt3aOwnershipMigration.ValidateGraphAsync(db, legacy);
if (before != JsonSerializer.Serialize(await Mt3aOwnershipMigration.SnapshotAsync(db))) throw new InvalidOperationException("Legacy digest changed");
Console.WriteLine("Existing MT3A SQL restored on electronic_register_test; legacy cell digests preserved; holding provisioning.");
