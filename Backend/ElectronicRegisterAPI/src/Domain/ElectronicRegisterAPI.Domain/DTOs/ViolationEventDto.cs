using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record ViolationEventDto(
    Guid EventId,
    string Type,
    DateTime OccurredAt,
    string? Metadata
);
