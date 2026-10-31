using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Onboarding;

public sealed record InvitationDelivery(
    Guid InstitutionId,
    Guid InvitationId,
    Guid RecipientUserId,
    string Address,
    string Challenge,
    DateTime ExpiresAtUtc
)
{
    public override string ToString() => "InvitationDelivery [recipient and proof redacted]";
}
