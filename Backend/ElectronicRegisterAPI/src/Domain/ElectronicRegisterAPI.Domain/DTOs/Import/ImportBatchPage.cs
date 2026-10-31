using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportBatchPage(
    int PageNumber,
    int PageSize,
    long TotalCount,
    IReadOnlyList<ImportBatch> Items
);
