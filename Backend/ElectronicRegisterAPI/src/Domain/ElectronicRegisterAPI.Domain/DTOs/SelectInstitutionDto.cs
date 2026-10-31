using System.ComponentModel.DataAnnotations;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed class SelectInstitutionDto
{
    public Guid InstitutionId { get; set; }
    public Guid MembershipId { get; set; }

    // Optional assertion, never a source of privileges. Full membership roles remain in the token.
    [MaxLength(32)]
    public string? RequestedRole { get; set; }
}
