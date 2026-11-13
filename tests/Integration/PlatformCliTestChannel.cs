using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace ElectronicRegisterAPI.Tests.Integration;

// Test assembly only. Owner-installed private simulated inbox, never registered
// by the application or selected from a request. No challenge is logged.
public sealed class PlatformCliTestChannel(IConfiguration configuration) : IInvitationChannel
{
    public bool IsOperational=>Directory.Exists(configuration["TestInbox"]);
    public Task DeliverAsync(InvitationDelivery delivery,CancellationToken ct)=>
        File.WriteAllTextAsync(Path.Combine(configuration["TestInbox"]!,delivery.InvitationId+".json"),JsonSerializer.Serialize(delivery),ct);
}
