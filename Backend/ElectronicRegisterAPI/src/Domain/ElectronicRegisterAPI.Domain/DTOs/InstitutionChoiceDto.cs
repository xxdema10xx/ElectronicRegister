using System.ComponentModel.DataAnnotations;

namespace ElectronicRegisterAPI.Domain.DTOs;

public sealed record InstitutionChoiceDto(
    Guid InstitutionId,
    Guid MembershipId,
    string Name,
    IReadOnlyList<string> Roles
);
