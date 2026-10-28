using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.DTOs.Import;

public sealed record ImportTable(
    string Sha256,
    string Delimiter,
    IReadOnlyList<string> Headers,
    IReadOnlyList<ImportRawRow> Rows
);
