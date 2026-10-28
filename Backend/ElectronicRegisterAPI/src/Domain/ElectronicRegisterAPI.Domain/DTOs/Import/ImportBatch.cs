using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportBatch(
    Guid Id,
    ImportCategory Category,
    string State,
    long Revision,
    string FileSha256,
    string PreviewDigest,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    long? PolicyRevision,
    IReadOnlyList<ImportIssue> Warnings,
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<ImportColumnMapping>? Mapping = null
);
