using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Results;

public sealed record MembershipMutationResult(
    Guid EventId,
    long Revision,
    MembershipStatus Status,
    IReadOnlyList<InstitutionRole> Roles
);
