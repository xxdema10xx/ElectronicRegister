using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed class InstitutionViolationDto
{
    public Guid EventId { get; set; }
    public string Type { get; set; } = "";
    public string? Metadata { get; set; }
}
