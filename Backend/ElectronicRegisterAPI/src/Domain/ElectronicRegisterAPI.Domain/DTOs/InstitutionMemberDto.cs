namespace ElectronicRegisterAPI.Domain.DTOs;

// Excludes global legacy roles, profile links and authentication data.
public sealed record InstitutionMemberDto(Guid MembershipId, Guid UserId, string Email,
    string Status, IReadOnlyList<string> Roles, DateTime JoinedAt, DateTime? SuspendedAt);
