using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

// AutoSubmitted is additive: true once the attempt has been submitted by the server
// because the configured violation threshold was reached (SubmitReason.Violations).
public sealed record ViolationReceiptDto(
    Guid EventId,
    int ViolationCount,
    bool Duplicate,
    bool AutoSubmitted = false
);
