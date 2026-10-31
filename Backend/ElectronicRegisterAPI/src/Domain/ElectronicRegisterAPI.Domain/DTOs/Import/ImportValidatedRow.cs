using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportValidatedRow(
    int RowNumber,
    ImportRowStatus Status,
    string? FirstName,
    string? LastName,
    string? ClassReference,
    string? ExternalReference,
    string? EmailPreview,
    IReadOnlyList<ImportIssue> Issues
);
