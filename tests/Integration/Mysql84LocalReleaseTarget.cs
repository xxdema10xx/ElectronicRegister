using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// Read-only attestation for the Python release runner's local ephemeral server.
// Deliberately never registers the target in the fixture purge capability map.
internal static class Mysql84LocalReleaseTarget
{
    internal static bool IsLocal(JsonElement config) => config.TryGetProperty("isolatedLocal",out var flag) && flag.GetBoolean();
    internal static async Task<MySqlConnectionStringBuilder> ConnectionAsync(JsonElement config,string database)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Local release attestation requires Windows");
        Assert.True(IsLocal(config));
        Assert.NotEqual("true",Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_CONTEXT")));
        var id=config.GetProperty("mysqlContainer").GetString()!;var protectedId=config.GetProperty("protectedContainer").GetString()!;
        Assert.Matches("^[a-f0-9]{64}$",id);Assert.NotEqual(protectedId,id);
        var start=new ProcessStartInfo("docker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"inspect",id,protectedId})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));Assert.Equal(0,process.ExitCode);await error;
        using var document=JsonDocument.Parse(await output);var target=document.RootElement[0];var original=document.RootElement[1];
        Assert.Equal(id,target.GetProperty("Id").GetString());Assert.Equal(protectedId,original.GetProperty("Id").GetString());
        Assert.Equal("/"+config.GetProperty("containerName").GetString(),target.GetProperty("Name").GetString());
        Assert.True(target.GetProperty("State").GetProperty("Running").GetBoolean());
        Assert.Equal("mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b",target.GetProperty("Config").GetProperty("Image").GetString());
        Assert.Equal(config.GetProperty("localAttestation").GetString(),target.GetProperty("Config").GetProperty("Labels").GetProperty("er84.local.attestation").GetString());
        var mounts=target.GetProperty("Mounts");Assert.Equal(1,mounts.GetArrayLength());var mount=mounts[0];
        Assert.Equal("volume",mount.GetProperty("Type").GetString());Assert.Equal(config.GetProperty("volumeName").GetString(),mount.GetProperty("Name").GetString());Assert.Equal("/var/lib/mysql",mount.GetProperty("Destination").GetString());
        Assert.DoesNotContain(original.GetProperty("Mounts").EnumerateArray(),m=>m.GetProperty("Source").GetString()==mount.GetProperty("Source").GetString());
        var port=config.GetProperty("port").GetUInt32();Assert.DoesNotContain(port,new uint[]{3306,3307,13306});
        var binding=target.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("3306/tcp");Assert.Single(binding.EnumerateArray());
        Assert.Equal("127.0.0.1",binding[0].GetProperty("HostIp").GetString());Assert.Equal(port.ToString(),binding[0].GetProperty("HostPort").GetString());
        Assert.Equal("127.0.0.1",config.GetProperty("host").GetString());Assert.True(database.StartsWith("er84_",StringComparison.Ordinal)||database=="electronic_register_test");
        var defaults=Path.GetFullPath(config.GetProperty("defaultsFile").GetString()!);
        var acl=new FileInfo(defaults).GetAccessControl();var trusted=new[]{WindowsIdentity.GetCurrent().User!.Value,"S-1-5-18","S-1-5-32-544"};
        Assert.Contains(acl.GetOwner(typeof(SecurityIdentifier))!.Value,trusted);
        Assert.DoesNotContain(acl.GetAccessRules(true,true,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>(),r=>r.AccessControlType==AccessControlType.Allow&&!trusted.Contains(r.IdentityReference.Value)&&(r.FileSystemRights&FileSystemRights.ReadData)!=0);
        var password=(await File.ReadAllLinesAsync(defaults)).Single(x=>x.StartsWith("password=",StringComparison.Ordinal))[9..];
        var builder=new MySqlConnectionStringBuilder{Server="127.0.0.1",Port=port,Database=database,UserID="root",Password=password,SslMode=MySqlSslMode.Required};
        await using var c=new MySqlConnection(builder.ConnectionString);await c.OpenAsync();
        await using var cmd=new MySqlCommand("SELECT VERSION(),@@server_uuid,@@hostname,@@port,@@datadir,DATABASE()",c);await using var reader=await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());Assert.Equal("8.4.13",reader.GetString(0));Assert.Equal(config.GetProperty("serverUuid").GetString(),reader.GetString(1));Assert.Equal(config.GetProperty("serverHost").GetString(),reader.GetString(2));Assert.Equal(3306u,reader.GetUInt32(3));Assert.Equal("/var/lib/mysql/",reader.GetString(4));Assert.Equal(database,reader.GetString(5));
        return builder;
    }
}
