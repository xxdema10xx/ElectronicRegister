namespace ElectronicRegisterAPI.Domain.Models;

// TemporaryPassword is present only in the response of the request that created the
// account; it is never persisted, so a replay of the same EventId returns null.
public sealed record InstitutionAccountCreated(
    Guid EventId,
    Guid UserId,
    Guid MembershipId,
    string[] Roles,
    Guid? StudentId,
    Guid? TeacherId,
    bool Replayed,
    string? TemporaryPassword
);
