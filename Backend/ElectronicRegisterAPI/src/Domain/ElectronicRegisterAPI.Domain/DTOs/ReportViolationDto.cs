using System.Text.Json;

namespace ElectronicRegisterAPI.Domain.DTOs;

public class ReportViolationDto
{
    public required string Type { get; set; }
    public JsonElement? Metadata { get; set; }
}
