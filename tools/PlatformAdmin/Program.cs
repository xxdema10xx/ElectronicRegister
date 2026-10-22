using System.Text.Json;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Application.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MySqlConnector;
using ElectronicRegisterAPI.Infrastructure.Security;

var operationJson=new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
operationJson.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<PlatformAction>(allowIntegerValues:false));

// Signing uses an explicitly selected, owner-installed Windows certificate key.
// It never registers or authorizes its public key in the authority registry.
if(args.Length==4 && args[0]=="sign")
{
    try
    {
        if(!OperatingSystem.IsWindows())throw new UnauthorizedAccessException();
        using var store=new X509Store(StoreName.My,StoreLocation.CurrentUser);store.Open(OpenFlags.ReadOnly);
        var certificates=store.Certificates.Find(X509FindType.FindByThumbprint,args[1],false);
        if(certificates.Count!=1)throw new UnauthorizedAccessException();
        using var rsa=certificates[0].GetRSAPrivateKey()??throw new UnauthorizedAccessException();
        if(rsa.KeySize<3072)throw new UnauthorizedAccessException();
        var op=JsonSerializer.Deserialize<SignedPlatformOperation>(await File.ReadAllTextAsync(args[2]),operationJson)??throw new ArgumentException();
        if(op.EventId==Guid.Empty)throw new ArgumentException("Explicit stable EventId required.");
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        op=op with{Nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),IssuedAt=now,ExpiresAt=now+240};
        var signed=op with{Signature=Convert.ToBase64String(rsa.SignData(PlatformSigning.Bytes(op),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))};
        await using var output=new FileStream(args[3],FileMode.CreateNew,FileAccess.Write,FileShare.None);
        await JsonSerializer.SerializeAsync(output,signed,operationJson);return 0;
    }
    catch(Exception){Console.Error.WriteLine("Signing refused. Certificate/key authorization remains an explicit owner action.");return 1;}
}

// No HTTP host, key generation, authority registration or private-key loading here.
// Registry and connection configuration are installed by the platform owner.
if(args.Length!=2) {Console.Error.WriteLine("Usage: PlatformAdmin <trusted-admin-config.json> <signed-operation.json>");return 2;}
try
{
    var file=Path.GetFullPath(args[0]);
    if(!OperatingSystem.IsWindows())throw new UnauthorizedAccessException("Protected Windows administrative host required.");
    var acl=new FileInfo(file).GetAccessControl();
    var trusted=new[]{System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value,"S-1-5-18","S-1-5-32-544"};
    if(!trusted.Contains((acl.GetOwner(typeof(System.Security.Principal.SecurityIdentifier))?.Value ?? "")))throw new UnauthorizedAccessException();
    foreach(System.Security.AccessControl.FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(System.Security.Principal.SecurityIdentifier)))
        if(rule.AccessControlType==System.Security.AccessControl.AccessControlType.Allow && !trusted.Contains(rule.IdentityReference.Value) &&
            (rule.FileSystemRights & (System.Security.AccessControl.FileSystemRights.ReadData|System.Security.AccessControl.FileSystemRights.WriteData|System.Security.AccessControl.FileSystemRights.AppendData|System.Security.AccessControl.FileSystemRights.WriteAttributes|System.Security.AccessControl.FileSystemRights.WriteExtendedAttributes|System.Security.AccessControl.FileSystemRights.Delete|System.Security.AccessControl.FileSystemRights.DeleteSubdirectoriesAndFiles|System.Security.AccessControl.FileSystemRights.ChangePermissions|System.Security.AccessControl.FileSystemRights.TakeOwnership))!=0)
            throw new UnauthorizedAccessException("Authority configuration is accessible to a broad principal.");
    var raw=await File.ReadAllTextAsync(args[1]);
    using var document=JsonDocument.Parse(raw);
    static void Unique(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object) {if(value.EnumerateObject().GroupBy(p=>p.Name).Any(g=>g.Count()!=1))throw new ArgumentException();foreach(var p in value.EnumerateObject())Unique(p.Value);}
        if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())Unique(item);
    }
    Unique(document.RootElement);
    var operation=JsonSerializer.Deserialize<SignedPlatformOperation>(raw,operationJson)??throw new ArgumentException();
    var config=new ConfigurationBuilder().AddJsonFile(file,false,false).Build();
    var connection=new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")??throw new ArgumentException("Private connection configuration required."));
    if(connection.Server!="127.0.0.1" || connection.Port!=13306 || connection.Database!="electronic_register_test")throw new UnauthorizedAccessException("Explicit isolated test target required.");
    await using(var sql=new MySqlConnection(connection.ConnectionString))
    {
        await sql.OpenAsync();await using var command=new MySqlCommand("SELECT VERSION(),@@hostname,@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE()",sql);
        await using var r=await command.ExecuteReaderAsync();
        if(!await r.ReadAsync() || r.GetString(0)!="5.7.24" || !r.GetString(1).Equals(Environment.MachineName,StringComparison.OrdinalIgnoreCase) || r.GetInt32(2)!=13306 ||
           !r.GetString(3).EndsWith("/migrations/mt4/private/mt5a-mamp-13306/data/",StringComparison.OrdinalIgnoreCase) || r.GetString(4)!="electronic_register_test")throw new UnauthorizedAccessException();
    }
    var services=new ServiceCollection().AddSingleton<IConfiguration>(config).AddLogging();services.AddInfrastructure(config);services.AddApplicationManagers();
    await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();
    var result=operation.Action is PlatformAction.Suspend or PlatformAction.Reactivate or PlatformAction.Archive or PlatformAction.RequestRecovery or PlatformAction.ApproveRecovery or PlatformAction.RevokeRecovery or PlatformAction.ReissueRecovery
        ? await scope.ServiceProvider.GetRequiredService<IInstitutionGovernanceManager>().ExecuteAsync(operation,CancellationToken.None)
        : await scope.ServiceProvider.GetRequiredService<IOnboardingRepository>().PlatformAsync(operation,CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(result));return 0;
}
catch(Exception) { Console.Error.WriteLine("Administrative operation refused or failed. No automatic retry. Use private diagnostics.");return 1; }
