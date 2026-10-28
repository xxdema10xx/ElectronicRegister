using ElectronicRegisterAPI.Domain.DTOs.Onboarding;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

// The provider is installed by the trusted host, never selected by request data.
public interface IInvitationChannel
{
    bool IsOperational { get; }

    Task DeliverAsync(InvitationDelivery delivery, CancellationToken ct);
}
