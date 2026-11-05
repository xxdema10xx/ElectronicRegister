using MySqlConnector;

namespace ElectronicRegister.OwnerProvisioning;

// Usage (the connection string never appears on the command line or in any output):
//   ER_OWNER_MYSQL_CONNECTION=... owner-provisioning create-institution --code C --name N --admin-email E --expected-server-uuid U [--reason R]
//   ER_OWNER_MYSQL_CONNECTION=... owner-provisioning reset-password --email E --expected-server-uuid U [--reason R]
// Instead of the environment variable, --connection-file F reads it from a file outside any git work tree.
public static class OwnerProvisioningCli
{
    public const string ConnectionVariable = "ER_OWNER_MYSQL_CONNECTION";
    public const string Release = "er-quality-2-mysql84";

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, Func<string, string?> environment)
    {
        if (args.Length == 0 || args[0] is not ("create-institution" or "reset-password"))
            return Usage(error);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length || options.ContainsKey(args[i]))
                return Usage(error);
            options[args[i]] = args[i + 1];
        }
        string Option(string name) => options.TryGetValue(name, out var value) ? value : throw new ArgumentException("Opzione obbligatoria mancante: " + name);
        try
        {
            var allowed = args[0] == "create-institution" ? new[] { "--code", "--name", "--admin-email" } : new[] { "--email" };
            if (options.Keys.Except(allowed.Concat(new[] { "--expected-server-uuid", "--reason", "--connection-file" })).Any())
                return Usage(error);
            await using var connection = new MySqlConnection(ConnectionString(options.GetValueOrDefault("--connection-file"), environment));
            await connection.OpenAsync();
            await RequireTargetAsync(connection, Option("--expected-server-uuid"));
            var reason = options.GetValueOrDefault("--reason") ?? (args[0] == "create-institution" ? "Owner provisioning" : "Owner password reset");
            if (args[0] == "create-institution")
            {
                var result = await OwnerProvisioner.CreateInstitutionAsync(connection, Option("--code"), Option("--name"), Option("--admin-email"), reason);
                output.WriteLine("status: " + result.Status);
                output.WriteLine("institution: " + result.InstitutionId);
                output.WriteLine("admin user: " + result.UserId);
                output.WriteLine("membership: " + result.MembershipId);
                output.WriteLine(result.TemporaryPassword is null
                    ? "temporary password: not shown (already provisioned; use reset-password if it was lost)"
                    : "temporary password (shown once, never stored): " + result.TemporaryPassword);
            }
            else
            {
                var result = await OwnerProvisioner.ResetPasswordAsync(connection, Option("--email"), reason);
                output.WriteLine("status: reset");
                output.WriteLine("user: " + result.UserId);
                output.WriteLine("previous tokens revoked: identity generation " + result.Generation);
                output.WriteLine("temporary password (shown once, never stored): " + result.TemporaryPassword);
            }
            return 0;
        }
        catch (TargetRejectedException e) { error.WriteLine("Target rifiutato: " + e.Message); return 3; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { error.WriteLine("Errore: " + e.Message); return 4; }
        catch (MySqlException e) { error.WriteLine("Errore del database (codice " + e.Number + "); nessuna modifica applicata."); return 5; }
    }

    private static int Usage(TextWriter error)
    {
        error.WriteLine("Uso: create-institution --code C --name N --admin-email E --expected-server-uuid U [--reason R] [--connection-file F]");
        error.WriteLine("     reset-password --email E --expected-server-uuid U [--reason R] [--connection-file F]");
        error.WriteLine("Connessione: variabile " + ConnectionVariable + " oppure --connection-file fuori dal repository.");
        return 2;
    }

    private static string ConnectionString(string? file, Func<string, string?> environment)
    {
        if (file is null)
            return environment(ConnectionVariable) is { Length: > 0 } value ? value : throw new ArgumentException("Connessione mancante: impostare " + ConnectionVariable + " o --connection-file.");
        var path = Path.GetFullPath(file);
        // Secrets never live in a repository: refuse a file inside any git work tree.
        for (var dir = Path.GetDirectoryName(path); dir is not null; dir = Path.GetDirectoryName(dir))
            if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
                throw new ArgumentException("Il file di connessione deve stare fuori da qualsiasi repository git.");
        return File.ReadAllText(path).Trim();
    }

    // The operator names the exact server; the tool refuses anything else or a schema
    // that is not the reviewed MySQL 8.4 release.
    private static async Task RequireTargetAsync(MySqlConnection connection, string expectedUuid)
    {
        await using var cmd = new MySqlCommand("SELECT @@server_uuid,VERSION(),DATABASE(),(SELECT COUNT(*) FROM er_schema_release WHERE version=@release)", connection);
        cmd.Parameters.AddWithValue("@release", Release);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) throw new TargetRejectedException("identità del server non leggibile.");
        if (!string.Equals(r.GetString(0), expectedUuid.Trim(), StringComparison.OrdinalIgnoreCase)) throw new TargetRejectedException("server_uuid diverso da quello atteso.");
        if (!r.GetString(1).StartsWith("8.4.", StringComparison.Ordinal)) throw new TargetRejectedException("versione MySQL diversa da 8.4.");
        if (r.IsDBNull(2)) throw new TargetRejectedException("nessun database selezionato.");
        if (Convert.ToInt64(r.GetValue(3)) != 1) throw new TargetRejectedException("schema diverso dalla release " + Release + ".");
    }

    private sealed class TargetRejectedException(string message) : Exception(message);
}
