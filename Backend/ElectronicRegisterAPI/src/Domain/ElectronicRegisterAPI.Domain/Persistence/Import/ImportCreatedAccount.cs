using ElectronicRegisterAPI.Domain.DTOs.Import;

namespace ElectronicRegisterAPI.Domain.Persistence.Import;

public sealed record ImportCreatedAccount(
    Guid StudentId,
    ImportAccountRow Account
);
