using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// er-grade-scale-1 (migrations/grade-scale): one-time conversion of stored tenths into thirtieths,
// the reviewed grade DDL of the successor and its read-only 002 validation.
[Collection(IntegrationCollection.Name)]
public sealed class GradeScaleMigrationTests(IntegrationFixture fixture)
{
    private static readonly string Root = FindRoot();
    private static string FindRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "migrations", "grade-scale", "001_expand.sql"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("Grade scale migration unavailable");
    }
    private static string Migration(string file) => File.ReadAllText(Path.Combine(Root, "migrations", "grade-scale", file)).Replace("\r\n", "\n");
    private static JsonElement Json(params string[] path) => JsonDocument.Parse(File.ReadAllText(Path.Combine(new[] { Root }.Concat(path).ToArray()))).RootElement;

    private async Task<MySqlConnection> Open(string? database = null)
    {
        var builder = new MySqlConnectionStringBuilder(fixture.ConnectionString) { AllowUserVariables = true };
        if (database is not null) builder.Database = database;
        var c = new MySqlConnection(builder.ConnectionString);
        await c.OpenAsync();
        return c;
    }

    private static async Task Execute(MySqlConnection c, string sql, params (string Name, object? Value)[] values)
    {
        await using var command = new MySqlCommand(sql, c);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> Scalar(MySqlConnection c, string sql, params (string Name, object? Value)[] values)
    {
        await using var command = new MySqlCommand(sql, c);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return await command.ExecuteScalarAsync();
    }

    // Predecessor (er-quality-2-mysql84) DDL without foreign keys: the probe database has no parents.
    private static string WithoutForeignKeys(string ddl) => Regex.Replace(ddl.Replace("\r\n", "\n"), @"\n  CONSTRAINT `[^`]+` FOREIGN KEY [^\n]*", "").Replace(",\n)", "\n)");

    [Fact]
    public async Task ConversionMapsTenthsToThirtiethsOnceAndKeepsPassedCoherent()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        var probe = "er_grade_scale_" + Guid.NewGuid().ToString("N")[..12];
        await using (var admin = await Open())
            await Execute(admin, "CREATE DATABASE `" + probe + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            await using var c = await Open(probe);
            var contract = Json("migrations", "mysql84-remediation", "contract.json");
            foreach (var table in new[] { "grade", "exam_submission", "er_schema_release" })
                await Execute(c, WithoutForeignKeys(contract.GetProperty("tables").GetProperty(table).GetString()!));
            // The MT3A owner guard stays active during the conversion.
            foreach (var trigger in new[] { "mt3a_grade_update", "mt3a_exam_submission_update" })
                await Execute(c, contract.GetProperty("triggers").GetProperty(trigger).GetProperty("statement").GetString()!);

            var owner = Guid.NewGuid();
            var tenths = new[] { 1m, 5.5m, 5.99m, 6m, 7.33m, 10m };
            var expected = new[] { 3m, 17m, 17m, 18m, 22m, 30m };
            var grades = tenths.Select(_ => Guid.NewGuid()).ToArray();
            for (var i = 0; i < tenths.Length; i++)
            {
                await Execute(c, "INSERT INTO grade(id,student_id,class_subject_id,value,date,institution_id) VALUES(@id,@s,@cs,@v,'2026-01-15',@o)",
                    ("@id", grades[i]), ("@s", Guid.NewGuid()), ("@cs", Guid.NewGuid()), ("@v", tenths[i]), ("@o", owner));
                // Passed deliberately stale (inverted) for every graded submission; suggested = official.
                await Execute(c, "INSERT INTO exam_submission(id,exam_id,student_id,status,started_at,deadline_at,violation_count,last_client_revision,suggested_grade,passed,grade_id,institution_id,grade_institution_id) " +
                                 "VALUES(@id,@e,@s,'finalized',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),0,0,@v,@p,@g,@o,@o)",
                    ("@id", Guid.NewGuid()), ("@e", Guid.NewGuid()), ("@s", Guid.NewGuid()), ("@v", tenths[i]), ("@p", tenths[i] < 6m), ("@g", grades[i]), ("@o", owner));
            }
            // A submission without an official grade keeps its provisional passed.
            await Execute(c, "INSERT INTO exam_submission(id,exam_id,student_id,status,started_at,deadline_at,violation_count,last_client_revision,suggested_grade,passed,institution_id) " +
                             "VALUES(UUID(),UUID(),UUID(),'pending_review',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),0,0,8.5,1,@o)", ("@o", owner));

            await Execute(c, Migration("001_expand.sql"));

            async Task<Dictionary<Guid, (decimal Grade, decimal Suggested, bool Passed)>> State()
            {
                var state = new Dictionary<Guid, (decimal, decimal, bool)>();
                await using var command = new MySqlCommand("SELECT g.id,g.value,s.suggested_grade,s.passed,g.institution_id FROM grade g JOIN exam_submission s ON s.grade_id=g.id", c);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Assert.Equal(owner, reader.GetGuid(4));
                    state[reader.GetGuid(0)] = (reader.GetDecimal(1), reader.GetDecimal(2), reader.GetBoolean(3));
                }
                return state;
            }
            var converted = await State();
            for (var i = 0; i < grades.Length; i++)
                Assert.Equal((expected[i], expected[i], expected[i] >= 18m), converted[grades[i]]);
            Assert.Equal(26m, Convert.ToDecimal(await Scalar(c, "SELECT suggested_grade FROM exam_submission WHERE grade_id IS NULL")));
            Assert.Equal(1L, Convert.ToInt64(await Scalar(c, "SELECT passed FROM exam_submission WHERE grade_id IS NULL")));
            Assert.Equal(1L, Convert.ToInt64(await Scalar(c, "SELECT COUNT(*) FROM er_schema_release WHERE version='er-grade-scale-1'")));

            // Replaying the guarded conversion changes nothing; replaying the file stops at the ALTER.
            var sql = Migration("001_expand.sql");
            var start = sql.IndexOf("START TRANSACTION;", StringComparison.Ordinal);
            var updates = sql[start..sql.IndexOf("INSERT INTO er_schema_release", StringComparison.Ordinal)];
            await Execute(c, updates);
            Assert.Equal(converted, await State());
            Assert.Equal(1060, (await Assert.ThrowsAsync<MySqlException>(() => Execute(c, sql))).Number);
            Assert.Equal(converted, await State());
        }
        finally
        {
            await using var admin = await Open();
            await Execute(admin, "DROP DATABASE `" + probe + "`");
        }
    }

    [Fact]
    public async Task FixtureCarriesTheReviewedGradeDdlAndInventory()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var c = await Open();
        var target = Json("migrations", "grade-scale", "predecessor-contract.json").GetProperty("target");
        await using (var command = new MySqlCommand("SHOW CREATE TABLE `grade`", c))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(target.GetProperty("tables").GetProperty("grade").GetString(), reader.GetString(1).Replace("\r\n", "\n"));
        }
        var inventory = target.GetProperty("inventory");
        Assert.Equal(inventory.GetProperty("tables").GetInt64(), Convert.ToInt64(await Scalar(c, "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()")));
        Assert.Equal(inventory.GetProperty("triggers").GetInt64(), Convert.ToInt64(await Scalar(c, "SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()")));
        Assert.Equal(inventory.GetProperty("fks").GetInt64(), Convert.ToInt64(await Scalar(c, "SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE()")));
        Assert.Equal(inventory.GetProperty("indexes").GetInt64(), Convert.ToInt64(await Scalar(c, "SELECT COUNT(*) FROM (SELECT DISTINCT TABLE_NAME,INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()) x")));
    }

    [Fact]
    public async Task ValidationScriptReportsZeroViolations()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        await using var c = await Open();
        var checks = 0;
        foreach (var statement in Migration("002_validate.sql").Split(";\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!statement.TrimStart().StartsWith("SELECT", StringComparison.Ordinal) && !statement.Contains("\nSELECT", StringComparison.Ordinal)) continue;
            await using var command = new MySqlCommand(statement, c);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt64(1) == 0, reader.GetString(0));
            checks++;
        }
        Assert.Equal(7, checks);
    }
}
