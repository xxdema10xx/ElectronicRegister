using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using ElectronicRegisterAPI.Infrastructure.Persistence;

namespace ElectronicRegisterAPI.Tests.Integration
{
    // Creates the schema only on an empty database and verifies it against the EF model.
    // EnsureCreated never alters an existing schema, so a stale schema is reported instead of patched.
    // Nothing here drops databases or tables.
    internal static class TestDatabaseSchema
    {
        // Indexes the concurrency tests depend on, checked explicitly even if the EF model changes.
        private static readonly (string Table, string Index, string[] Columns, bool Unique)[] RequiredIndexes =
        {
            ("exam_submission", "idx_exam_submission_expiration", new[] { "status", "deadline_at" }, false),
            ("exam_submission", "uq_exam_submission_student", new[] { "exam_id", "student_id" }, true),
            ("exam_submission", "uq_exam_submission_grade", new[] { "grade_id" }, true),
            ("submission_answer", "uq_submission_answer", new[] { "submission_id", "question_id" }, true),
        };

        public static async Task EnsureAsync(string connectionString, ElectronicRegisterContext ctx)
        {
            var database = new MySqlConnectionStringBuilder(connectionString).Database;
            var serverOnly = new MySqlConnectionStringBuilder(connectionString) { Database = string.Empty };

            await using var connection = new MySqlConnection(serverOnly.ConnectionString);
            await connection.OpenAsync();

            var databaseExists = await CountAsync(connection,
                "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @db", database) > 0;

            var tableCount = databaseExists
                ? await CountAsync(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @db", database)
                : 0;

            if (tableCount == 0)
            {
                var created = await ctx.Database.EnsureCreatedAsync();
                if (!created)
                    throw new InvalidOperationException(
                        $"EnsureCreated non ha creato lo schema nel database vuoto '{database}'.");
            }

            await VerifyAsync(connection, database, ctx.Model);
        }

        private static async Task VerifyAsync(MySqlConnection connection, string database, IModel model)
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await ReadAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = @db", database,
                r => tables.Add(r.GetString(0)));

            var columns = new HashSet<(string, string)>(TableItemComparer.Instance);
            await ReadAsync(connection, "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = @db", database,
                r => columns.Add((r.GetString(0), r.GetString(1))));

            // (table, index) -> ordered columns + uniqueness
            var indexes = new Dictionary<(string, string), (List<string> Columns, bool Unique)>(TableItemComparer.Instance);
            await ReadAsync(connection,
                "SELECT table_name, index_name, column_name, non_unique FROM information_schema.statistics " +
                "WHERE table_schema = @db ORDER BY table_name, index_name, seq_in_index", database,
                r =>
                {
                    var key = (r.GetString(0), r.GetString(1));
                    if (!indexes.TryGetValue(key, out var index))
                    {
                        index = (new List<string>(), Convert.ToInt64(r.GetValue(3)) == 0);
                        indexes[key] = index;
                    }
                    index.Columns.Add(r.GetString(2));
                });

            var foreignKeys = new HashSet<(string, string)>(TableItemComparer.Instance);
            await ReadAsync(connection,
                "SELECT table_name, constraint_name FROM information_schema.table_constraints " +
                "WHERE table_schema = @db AND constraint_type = 'FOREIGN KEY'", database,
                r => foreignKeys.Add((r.GetString(0), r.GetString(1))));

            var problems = new List<string>();

            void CheckIndex(string table, string name, IReadOnlyList<string> expectedColumns, bool unique)
            {
                if (!indexes.TryGetValue((table, name), out var actual))
                {
                    problems.Add($"indice mancante {table}.{name} ({string.Join(", ", expectedColumns)})");
                    return;
                }

                if (!actual.Columns.SequenceEqual(expectedColumns, StringComparer.OrdinalIgnoreCase))
                    problems.Add($"indice {table}.{name}: colonne ({string.Join(", ", actual.Columns)}), attese ({string.Join(", ", expectedColumns)})");

                if (unique && !actual.Unique)
                    problems.Add($"indice {table}.{name} non è UNIQUE");
            }

            foreach (var entityType in model.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table is null) continue;

                var store = StoreObjectIdentifier.Table(table, entityType.GetSchema());

                if (!tables.Contains(table))
                {
                    problems.Add($"tabella mancante {table}");
                    continue;
                }

                foreach (var property in entityType.GetProperties())
                {
                    var column = property.GetColumnName(store);
                    if (column != null && !columns.Contains((table, column)))
                        problems.Add($"colonna mancante {table}.{column}");
                }

                var primaryKey = entityType.FindPrimaryKey();
                if (primaryKey != null)
                    CheckIndex(table, "PRIMARY", primaryKey.Properties.Select(p => p.GetColumnName(store)!).ToList(), true);

                foreach (var index in entityType.GetIndexes())
                {
                    var name = index.GetDatabaseName(store);
                    if (name is null) continue;
                    CheckIndex(table, name, index.Properties.Select(p => p.GetColumnName(store)!).ToList(), index.IsUnique);
                }

                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    var name = foreignKey.GetConstraintName();
                    if (name != null && !foreignKeys.Contains((table, name)))
                        problems.Add($"vincolo FK mancante {table}.{name}");
                }
            }

            foreach (var (table, index, indexColumns, unique) in RequiredIndexes)
                CheckIndex(table, index, indexColumns, unique);

            if (problems.Count > 0)
                throw new InvalidOperationException(
                    $"Lo schema del database '{database}' non corrisponde al modello EF. " +
                    "EnsureCreated non aggiorna schemi esistenti: ricreare manualmente il database di test vuoto. " +
                    "Problemi: " + string.Join("; ", problems.Distinct()));
        }

        private static async Task<long> CountAsync(MySqlConnection connection, string sql, string database)
        {
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@db", database);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        private static async Task ReadAsync(MySqlConnection connection, string sql, string database, Action<MySqlDataReader> onRow)
        {
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@db", database);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                onRow(reader);
        }

        // MySQL identifiers are compared case-insensitively (lower_case_table_names on Windows).
        private sealed class TableItemComparer : IEqualityComparer<(string, string)>
        {
            public static readonly TableItemComparer Instance = new();

            public bool Equals((string, string) x, (string, string) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1) &&
                StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);

            public int GetHashCode((string, string) obj) =>
                HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
        }
    }
}
