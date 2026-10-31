using ElectronicRegisterAPI.Domain.Models;
using System.Collections.Frozen;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Authorization;

public sealed record MembershipAuthorization(
    Guid UserId,
    Guid InstitutionId,
    Guid MembershipId,
    string InstitutionName,
    InstitutionStatus InstitutionStatus,
    MembershipStatus Status,
    IReadOnlyList<InstitutionRole> Roles,
    long? SecurityGeneration = null
);
