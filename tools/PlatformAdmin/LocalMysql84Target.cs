using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

// Called only after the administrative configuration's Windows ACL is accepted.
internal static class LocalMysql84Target
{
    internal static async Task VerifyAsync(IConfiguration config, MySqlConnectionStringBuilder connection)
    {
        static void Require(bool condition) { if (!condition) throw new UnauthorizedAccessException("Local disposable server attestation refused."); }
        Require(OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true");
        Require(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_CONTEXT")));
        var id = config["TestDatabase:LocalMysql84:ContainerId"] ?? "";
        Require(Regex.IsMatch(id, "^[a-f0-9]{64}$"));
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("inspect"); start.ArgumentList.Add(id);
        using var process = Process.Start(start) ?? throw new UnauthorizedAccessException();
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch { process.Kill(entireProcessTree: true); throw; }
        Require(process.ExitCode == 0); await error;
        using var document = JsonDocument.Parse(await output);
        var container = document.RootElement[0]; var name = container.GetProperty("Name").GetString()!.TrimStart('/');
        Require(container.GetProperty("Id").GetString() == id && Regex.IsMatch(name, "^er84-dotnet-[a-f0-9]{32}$"));
        var settings = container.GetProperty("Config");
        Require(settings.GetProperty("Image").GetString() == "mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b");
        Require(settings.GetProperty("Labels").GetProperty("er84.local.attestation").GetString() == name);
        Require(container.GetProperty("State").GetProperty("Running").GetBoolean());
        var mounts = container.GetProperty("Mounts"); Require(mounts.GetArrayLength() == 1);
        var mount = mounts[0];
        Require(mount.GetProperty("Type").GetString() == "volume" && mount.GetProperty("Name").GetString() == name + "-data" && mount.GetProperty("Destination").GetString() == "/var/lib/mysql");
        var ports = container.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("3306/tcp");
        Require(ports.EnumerateArray().Any(p => p.GetProperty("HostPort").GetString() == connection.Port.ToString()));
        Require(connection.Server == "127.0.0.1" && connection.Database == "electronic_register_test" && connection.Port is >= 1024 and <= 65535 && connection.Port is not (3306 or 3307 or 13306));
        await using var sql = new MySqlConnection(connection.ConnectionString); await sql.OpenAsync();
        await using var command = new MySqlCommand("SELECT VERSION(),@@server_uuid,@@hostname,@@port,@@datadir,DATABASE()", sql);
        await using var reader = await command.ExecuteReaderAsync();
        Require(await reader.ReadAsync());
        Require(reader.GetString(0) == "8.4.13" && reader.GetString(1) == config["TestDatabase:LocalMysql84:ServerUuid"] && reader.GetString(2) == config["TestDatabase:LocalMysql84:ServerHost"] && reader.GetUInt32(3) == 3306 && reader.GetString(4) == "/var/lib/mysql/" && reader.GetString(5) == "electronic_register_test");
    }
}
