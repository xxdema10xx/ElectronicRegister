using ElectronicRegisterAPI.Domain.DTOs.Import;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Persistence.Import;

public sealed record ImportPayload(
    ImportTable Table,
    ImportPreviewSettings Settings,
    ImportMappingResult Mapping,
    IReadOnlyList<ImportPreparedRow> Rows,
    Guid? PolicyId,
    long? PolicyRevision,
    IReadOnlyList<ImportIssue> Warnings
);
