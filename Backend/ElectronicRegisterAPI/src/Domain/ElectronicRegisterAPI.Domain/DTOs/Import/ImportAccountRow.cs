using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

// Account of one imported row (CreatePlatformAccounts). Persisted results keep only the
// identifiers; Email is resolved when the result is read and TemporaryPassword exists only
// in the response of the confirming request. TemporaryPassword is null for a person already
// active on the platform (D-A) and is always serialized so every row has the same fields.
public sealed record ImportAccountRow(
    int RowNumber,
    Guid UserId,
    Guid MembershipId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Email,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? TemporaryPassword
);
