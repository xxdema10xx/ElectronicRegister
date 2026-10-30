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
foreach(var scenario in new[]{"valid","wrong-host","wrong-ca"})
{
    if(scenario!="valid"&&!valid)throw new InvalidOperationException("Positive TLS handshake failed; negative certificate results would be inconclusive.");
    var settings=new MySqlConnectionStringBuilder(original.ConnectionString);
    if(scenario=="wrong-host")settings.Server=wrongHost;if(scenario=="wrong-ca")settings.SslCa=wrongCa;
    try
    {
        await using var connection=new MySqlConnection(settings.ConnectionString);await connection.OpenAsync();
        if(scenario!="valid")throw new InvalidOperationException("Invalid certificate was accepted.");
        await using var protocol=new MySqlCommand("SHOW SESSION STATUS LIKE 'Ssl_version'",connection);await using var reader=await protocol.ExecuteReaderAsync();
        if(!await reader.ReadAsync()||reader.GetString(1)!="TLSv1.2")throw new InvalidOperationException("Required protocol was not negotiated.");
        await reader.DisposeAsync();await using var ready=new MySqlCommand("CALL er_schema_ready()",connection);
        if(Convert.ToInt32(await ready.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Destination schema not ready.");
        valid=true;results.Add(new{scenario,connected=true,protocol="TLSv1.2",schemaReady=true});
    }
    catch(MySqlException error)when(scenario!="valid")
    {
        bool certificate=false;
        for(Exception? inner=error;inner is not null;inner=inner.InnerException)if(inner is AuthenticationException)certificate=true;
        // DNS/connection refusal/timeout alone never proves certificate validation.
        if(!certificate)throw new InvalidOperationException("Negative scenario did not establish a TLS authentication failure.");
        results.Add(new{scenario,connected=false,certificateAuthenticationFailed=true});
    }
}
Console.WriteLine(JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
