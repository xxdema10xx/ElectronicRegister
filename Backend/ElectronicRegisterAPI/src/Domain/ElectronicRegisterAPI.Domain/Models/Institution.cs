using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public class Institution
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    // Internal application identifier, not an asserted official/ministry code.
    public required string Code { get; set; }
    public string? LegalTaxCode { get; set; }
    public string? Region { get; set; }
    public InstitutionStatus Status { get; set; } = InstitutionStatus.Provisioning;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
