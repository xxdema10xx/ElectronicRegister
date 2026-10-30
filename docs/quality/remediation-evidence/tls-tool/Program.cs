using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MySqlConnector;
if (args[0] == "probe")
{
    var metadata = System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[1]).TrimStart('\uFEFF')).RootElement;
    var run = metadata.GetProperty("Run").GetString()!;
    var connection = new MySqlConnectionStringBuilder { Server = "localhost", Port = metadata.GetProperty("Port").GetUInt32(), Database = "electronic_register_test", UserID = "root", Password = File.ReadAllText(Path.Combine(run,"password.txt")), SslMode = MySqlSslMode.VerifyFull, SslCa = Path.Combine(run,"certificates/ca.pem"), Pooling = false };
    var results = new List<object>();
    foreach (var scenario in new[] { "valid", "wrong-host", "wrong-ca" })
    {
        var settings = new MySqlConnectionStringBuilder(connection.ConnectionString);
        if (scenario == "wrong-host") settings.Server = "127.0.0.1";
        if (scenario == "wrong-ca") settings.SslCa = Path.Combine(run,"wrong-certificates/ca.pem");
        try
        {
            await using var sql = new MySqlConnection(settings.ConnectionString);
            await sql.OpenAsync();
            await using var command = new MySqlCommand("SHOW SESSION STATUS LIKE 'Ssl_cipher'", sql);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            results.Add(new { Scenario = scenario, Connected = true, Cipher = reader.GetString(1), Driver = typeof(MySqlConnection).Assembly.GetName().Version!.ToString() });
            if (scenario != "valid") throw new InvalidOperationException("Invalid certificate accepted.");
        }
        catch (MySqlException error)
        {
            results.Add(new { Scenario = scenario, Connected = false, ErrorType = error.GetType().Name, error.Number, InnerType = error.InnerException?.GetType().Name });
            if (scenario == "valid") Environment.ExitCode = 1;
        }
    }
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return;
}
var target = Path.GetFullPath(args[0]);
if (!target.StartsWith(Path.GetFullPath(".staff-private") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Directory.Exists(target))
    throw new InvalidOperationException("New private certificate directory required.");
Directory.CreateDirectory(target);
using var caKey = RSA.Create(3072);
var caRequest = new CertificateRequest("CN=ElectronicRegister disposable test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
using var key = RSA.Create(3072);
var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
var san = new SubjectAlternativeNameBuilder();
san.AddDnsName("localhost");
request.CertificateExtensions.Add(san.Build());
using var server = request.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(3), RandomNumberGenerator.GetBytes(16));
File.WriteAllText(Path.Combine(target,"ca.pem"), ca.ExportCertificatePem());
File.WriteAllText(Path.Combine(target,"server.pem"), server.ExportCertificatePem());
File.WriteAllText(Path.Combine(target,"server-key.pem"), key.ExportRSAPrivateKeyPem());
Console.WriteLine("Disposable CA and localhost/IP server certificate generated privately.");
