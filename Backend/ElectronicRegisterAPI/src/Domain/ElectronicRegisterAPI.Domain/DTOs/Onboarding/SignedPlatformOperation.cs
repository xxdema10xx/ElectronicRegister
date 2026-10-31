using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Onboarding;

public sealed record SignedPlatformOperation(
    string OperatorId,
    string KeyId,
    Guid EventId,
    string Nonce,
    long IssuedAt,
    long ExpiresAt,
    PlatformAction Action,
    PlatformIntent Intent,
    string Signature
);
