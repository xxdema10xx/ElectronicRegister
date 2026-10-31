using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Onboarding;

public sealed record AcceptInvitation(
    Guid InstitutionId,
    Guid InvitationId,
    Guid EventId,
    string Challenge
)
{
    public override string ToString() => "AcceptInvitation [proof redacted]";
}
