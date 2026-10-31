namespace ElectronicRegisterAPI.Domain.Models;

public sealed record IdentityAccount(
    Guid UserId,
    string Email,
    string PasswordHash,
    string? DisplayName,
    long Generation
);
