using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record ViolationReceiptDto(Guid EventId, int ViolationCount, bool Duplicate);
