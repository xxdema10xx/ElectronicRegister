using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportResult(
    Guid ImportId,
    long Revision,
    int CreatedCount,
    IReadOnlyList<ImportCreatedRow> Rows
);
