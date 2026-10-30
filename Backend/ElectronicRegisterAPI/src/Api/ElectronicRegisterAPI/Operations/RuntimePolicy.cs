using System.Net;
using System.Text;
using MySqlConnector;

namespace ElectronicRegisterAPI.Api.Operations;

// Configuration is owner-controlled, never request-controlled. LocalTest is confined
// to loopback and the dedicated test database; it cannot expose a public listener.
public sealed record RuntimePolicy(
    bool Local,
    string[] Origins,
    IPAddress[] Proxies,
    int SensitivePermitLimit
)
{
    public static RuntimePolicy Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var mode = configuration["Deployment:Mode"] ?? (environment.IsDevelopment() ? "LocalTest" : "");
        if (mode is not ("LocalTest" or "Staging" or "Production"))
            Fail();
        if (configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED") || configuration.GetValue<bool>("FORWARDEDHEADERS_ENABLED"))
            Fail();
        var local = mode == "LocalTest";
        var connection = new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection") ?? "");
        if (Encoding.UTF8.GetByteCount(configuration["Jwt:Key"] ?? "") < (local ? 32 : 48) || string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"]) || string.IsNullOrWhiteSpace(configuration["Jwt:Audience"]))
            Fail();
        var origins = configuration.GetSection("Transport:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Distinct(StringComparer.Ordinal).Count() != origins.Length || origins.Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || x.Contains('*') || (uri.Scheme != "https" && !(local && uri.Scheme == "http" && uri.IsLoopback))))
            Fail();
        var proxies = (configuration.GetSection("Transport:KnownProxies").Get<string[]>() ?? []).Select(IPAddress.Parse).ToArray();
        if (proxies.Any(p => p.Equals(IPAddress.Any) || p.Equals(IPAddress.IPv6Any)))
            Fail();
        if (local)
        {
            var localPort = configuration.GetValue<uint?>("Deployment:LocalTestDatabasePort") ?? 13306;
            if (localPort is < 1024 or > 65535 or 3306)
                Fail();
            var urls = configuration["urls"] ?? "http://127.0.0.1:5000";
            if (urls.Split(';').Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var u) || !u.IsLoopback) || proxies.Length != 0 || connection.Server != "127.0.0.1" || connection.Port != localPort || connection.Database != "electronic_register_test")
                Fail();
        }
        else
        {
            if (!environment.EnvironmentName.Equals(mode, StringComparison.Ordinal))
                Fail();
            var hosts = configuration["AllowedHosts"];
            var names = hosts?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (names.Length == 0 || names.Any(h => Uri.CheckHostName(h) == UriHostNameType.Unknown) || hosts!.Contains('*') || connection.UserID.Equals("root", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(connection.UserID) || string.IsNullOrWhiteSpace(connection.Password) || string.IsNullOrWhiteSpace(connection.Database) || connection.Port == 0 || connection.SslMode != MySqlSslMode.VerifyFull || string.IsNullOrWhiteSpace(connection.SslCa) || configuration["Readiness:ExpectedVersion"] is not { Length: > 0 } || configuration["Readiness:ExpectedHost"] is not { Length: > 0 } || configuration["Readiness:ExpectedDataDir"] is not { Length: > 0 } || !int.TryParse(configuration["Transport:HttpsPort"], out var port) || port is < 1 or > 65535 || configuration.GetValue<bool>("Identity:Legacy:Enabled"))
                Fail();
            // Rollout requires a separate, reviewed release configuration.
            foreach (var section in configuration.GetSection("Multitenancy").GetChildren())
                if (section.GetValue<bool>("Enabled") && !configuration.GetValue<bool>("Deployment:RolloutApproved"))
                    Fail();
        }

        var limit = configuration.GetValue<int?>("Transport:SensitivePermitLimit") ?? 60;
        if (limit < (local ? 3 : 10) || limit > 200)
            Fail();
        return new(local, origins, proxies, limit);
    }

    private static void Fail() => throw new InvalidOperationException("Runtime security configuration is incomplete or unsafe.");
}
