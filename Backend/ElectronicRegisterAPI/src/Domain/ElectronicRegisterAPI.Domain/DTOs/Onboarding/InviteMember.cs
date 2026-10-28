using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Onboarding;

public sealed record InviteMember(
    Guid EventId,
    Guid RecipientUserId,
    InstitutionRole[] Roles,
    string Reason
);
