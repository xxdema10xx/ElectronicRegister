namespace ElectronicRegisterAPI.Domain.DTOs.Import;

// One invalid row of a rejected import: physical row number and issue codes only.
public sealed record ImportRowError(
    int RowNumber,
    IReadOnlyList<string> Codes
);
