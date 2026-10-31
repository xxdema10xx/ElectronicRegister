using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Onboarding;

public sealed record PlatformIntent(
    Guid InstitutionId,
    Guid RecipientUserId,
    string Name,
    string Code,
    string LegalIdentity,
    string EvidenceReference,
    string Reason,
    Guid RecoveryId = default
);
