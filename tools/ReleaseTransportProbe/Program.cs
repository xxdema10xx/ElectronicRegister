using System.Security.Authentication;
using System.Text.Json;
using MySqlConnector;

// Read-only destination probe. Connection secrets stay in the mounted input file.
// Never accepts a failed positive handshake as evidence for certificate rejection.
if(args.Length!=1)throw new ArgumentException("Supply the private transport-probe JSON path.");
using var document=JsonDocument.Parse(File.ReadAllText(args[0]));var config=document.RootElement;
var original=new MySqlConnectionStringBuilder(config.GetProperty("connectionString").GetString()??throw new InvalidOperationException("Connection string required.")) {Pooling=false,ConnectionTimeout=5,DefaultCommandTimeout=5};
if(original.SslMode!=MySqlSslMode.VerifyFull||!File.Exists(original.SslCa))throw new InvalidOperationException("VerifyFull and an existing CA file are required.");
original["TlsVersion"]="TLS 1.2";
var wrongHost=config.GetProperty("wrongHost").GetString()!;var wrongCa=config.GetProperty("wrongCaFile").GetString()!;
if(wrongHost==original.Server||!File.Exists(wrongCa)||File.ReadAllBytes(wrongCa).SequenceEqual(File.ReadAllBytes(original.SslCa)))throw new InvalidOperationException("Independent wrong-host alias and wrong CA required.");
var results=new List<object>();bool valid=false;
var phase="driver-handshake";
try
{
foreach(var scenario in new[]{"valid","wrong-host","wrong-ca"})
{
    phase=scenario+"-handshake";
    if(scenario!="valid"&&!valid)throw new InvalidOperationException("Positive TLS handshake failed; negative certificate results would be inconclusive.");
    var settings=new MySqlConnectionStringBuilder(original.ConnectionString);
    if(scenario=="wrong-host")settings.Server=wrongHost;if(scenario=="wrong-ca")settings.SslCa=wrongCa;
    try
    {
        await using var connection=new MySqlConnection(settings.ConnectionString);await connection.OpenAsync();
        if(scenario!="valid")throw new InvalidOperationException("Invalid certificate was accepted.");
        phase="positive-protocol-and-schema";
        await using var protocol=new MySqlCommand("SHOW SESSION STATUS LIKE 'Ssl_version'",connection);await using var reader=await protocol.ExecuteReaderAsync();
        if(!await reader.ReadAsync()||reader.GetString(1)!="TLSv1.2")throw new InvalidOperationException("Required protocol was not negotiated.");
        await reader.DisposeAsync();await using var ready=new MySqlCommand("CALL er_schema_ready()",connection);
        if(Convert.ToInt32(await ready.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Destination schema not ready.");
        valid=true;results.Add(new{scenario,connected=true,protocol="TLSv1.2",schemaReady=true});
    }
    catch(MySqlException error)when(scenario!="valid")
    {
        bool certificate=false;
        var expectedError=scenario=="wrong-host"?"RemoteCertificateNameMismatch":"RemoteCertificateChainErrors";
        for(Exception? inner=error;inner is not null;inner=inner.InnerException)
            if(inner is AuthenticationException && inner.Message.Contains(expectedError,StringComparison.Ordinal))certificate=true;
        // DNS/connection refusal/timeout alone never proves certificate validation.
        if(!certificate)throw new InvalidOperationException("Negative scenario did not establish a TLS authentication failure.");
        results.Add(new{scenario,connected=false,certificateAuthenticationFailed=true,policyError=expectedError});
    }
}
phase="sql-regression";
var regression=await SqlRegression.Run(original);
Console.WriteLine(JsonSerializer.Serialize(new{tls=results,regression},new JsonSerializerOptions{WriteIndented=true}));
}
catch(Exception failure)
{
    // Bounded diagnostic fields; never serialize connection strings or exception messages.
    var chain=new List<object>();
    for(Exception? error=failure;error is not null;error=error.InnerException)
        chain.Add(new{type=error.GetType().Name,hresult=error.HResult,mysqlCode=error is MySqlException mysql?(int?)mysql.Number:null,
            nameMismatch=error.Message.Contains("RemoteCertificateNameMismatch",StringComparison.Ordinal),
            chainError=error.Message.Contains("RemoteCertificateChainErrors",StringComparison.Ordinal)});
    var diagnostic=JsonSerializer.Serialize(new{success=false,phase,chain,completed=results});
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!,"transport-failure.json"),diagnostic);
    Console.Error.WriteLine(diagnostic);
    Environment.ExitCode=1;
}
