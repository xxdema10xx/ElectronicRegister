using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Onboarding;

public sealed record ChangeInvitation(Guid EventId, Guid InvitationId, string Reason);
