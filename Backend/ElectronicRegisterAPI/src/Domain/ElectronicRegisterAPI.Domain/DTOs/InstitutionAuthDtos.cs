using System.ComponentModel.DataAnnotations;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record IdentityTicketDto(string Ticket, int ExpiresInSeconds);
public sealed record InstitutionChoiceDto(Guid InstitutionId, Guid MembershipId, string Name, IReadOnlyList<string> Roles);
public sealed class SelectInstitutionDto
{
    public Guid InstitutionId { get; set; }
    public Guid MembershipId { get; set; }
    // Optional assertion, never a source of privileges. Full membership roles remain in the token.
    [MaxLength(32)] public string? RequestedRole { get; set; }
}
public sealed record InstitutionAccessDto(string Token, int ExpiresInSeconds);
public sealed record TenantContextDto(Guid UserId, Guid InstitutionId, Guid MembershipId, IReadOnlyList<string> Roles);
