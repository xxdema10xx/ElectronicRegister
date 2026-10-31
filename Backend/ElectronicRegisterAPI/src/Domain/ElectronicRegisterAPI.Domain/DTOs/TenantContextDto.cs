using System.ComponentModel.DataAnnotations;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record TenantContextDto(
    Guid UserId,
    Guid InstitutionId,
    Guid MembershipId,
    IReadOnlyList<string> Roles
);
