namespace ElectronicRegisterAPI.Domain.DTOs;

// Excludes global legacy roles, profile links and authentication data. Revision is the
// tenant-scoped membership security generation only (no token, session or credential data).
public sealed record InstitutionMemberDto(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string Status,
    IReadOnlyList<string> Roles,
    DateTime JoinedAt,
    DateTime? SuspendedAt,
    // Persisted membership security generation: send it as ExpectedRevision to /mutations.
    long Revision
);
