using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Exceptions;

// Rows lists every invalid row with its issue codes (never cell values) for a 422 report.
public sealed class ImportInputException(string code, string message, IReadOnlyList<ImportRowError>? rows = null) : ArgumentException(message)
{
    public string Code { get; } = code;
    public IReadOnlyList<ImportRowError>? Rows { get; } = rows;
}
