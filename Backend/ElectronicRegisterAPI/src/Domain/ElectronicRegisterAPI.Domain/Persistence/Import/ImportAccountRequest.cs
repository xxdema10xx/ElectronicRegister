using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Persistence.Import;

// One person of a CreatePlatformAccounts batch, prepared before the global email mutex:
// protected fiscal code, platform address base and a BCrypt hash computed for every row.
public sealed record ImportAccountRequest(
    int RowNumber,
    string FirstName,
    string LastName,
    string LocalBase,
    Guid? ClassId,
    ProtectedFiscalCode FiscalCode,
    string PasswordHash,
    string TemporaryPassword
);
