using System.Text.Json.Serialization;

namespace ElectronicRegisterAPI.Domain.Models;

// Same shape whether the person is new, already active elsewhere or reactivated.
// TemporaryPassword is present only in the response of the request that created or
// reactivated the account; it is null for a person already active on the platform (D-A)
// and for every replay of the same EventId. It is never persisted. It is always serialized,
// also when null, so the response keeps the same set of fields in every branch.
public sealed record InstitutionAccountCreated(
    Guid EventId,
    Guid UserId,
    Guid MembershipId,
    string Email,
    string[] Roles,
    Guid? StudentId,
    Guid? TeacherId,
    bool Replayed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? TemporaryPassword
);
