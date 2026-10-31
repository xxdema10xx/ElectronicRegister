using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportRowsPage(
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyList<ImportPreparedRow> Items
);
