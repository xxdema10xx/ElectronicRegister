using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportMappingResult(IReadOnlyList<ImportColumnMapping> Columns, IReadOnlyList<ImportIssue> Issues);
