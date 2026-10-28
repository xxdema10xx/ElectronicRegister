using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.DTOs.Membership;

// Institution and actor come exclusively from the authenticated scoped context.
// ExpectedRevision is the persisted security generation, never a second counter.
public sealed record MembershipMutation(
    Guid MembershipId,
    Guid EventId,
    long ExpectedRevision,
    MembershipMutationAction Action,
    InstitutionRole? Role,
    string Reason
);
