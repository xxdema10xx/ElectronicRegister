using ElectronicRegisterAPI.Domain.Interfaces.Security;
using System.Runtime.Loader;
using System.Security.Cryptography;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElectronicRegisterAPI.Infrastructure.Security;

public static class InvitationChannelRegistration
{
    // Optional host adapter. Its public code hash/type/path are owner-installed
    // configuration, not values accepted from an HTTP/CLI operation payload.
    public static void AddOwnerInstalledInvitationChannel(this IServiceCollection services, IConfiguration config)
    {
        var path = config["InvitationChannel:AssemblyPath"];
        if (string.IsNullOrWhiteSpace(path))
            return; // closed provider stays installed
        var bytes = File.ReadAllBytes(Path.GetFullPath(path));
        if (Convert.ToHexString(SHA256.HashData(bytes)) != config["InvitationChannel:SHA256"])
            throw new UnauthorizedAccessException("Invitation adapter hash mismatch.");
        var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(bytes));
        var type = assembly.GetType(config["InvitationChannel:Type"] ?? "", true)!;
        if (!typeof(IInvitationChannel).IsAssignableFrom(type) || type.IsAbstract || !type.IsVisible)
            throw new UnauthorizedAccessException();
        services.AddScoped(typeof(IInvitationChannel), provider => ActivatorUtilities.CreateInstance(provider, type));
    }
}
