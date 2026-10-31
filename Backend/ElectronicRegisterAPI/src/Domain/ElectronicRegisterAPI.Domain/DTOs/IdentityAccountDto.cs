namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record IdentityAccountDto(
    Guid UserId,
    string Email,
    string? DisplayName,
    bool HasLocalPassword
);
