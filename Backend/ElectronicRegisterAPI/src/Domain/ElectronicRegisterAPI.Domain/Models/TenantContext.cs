using System.Collections.Frozen;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

// No legacy school profile is implied by a membership.
public sealed class TenantContext
{
    public Guid UserId { get; }
    public Guid InstitutionId { get; }
    public Guid MembershipId { get; }
    public IReadOnlySet<InstitutionRole> Roles { get; }
    public TenantContext(Guid userId, Guid institutionId, Guid membershipId, IEnumerable<InstitutionRole> roles)
    {
        var values = roles.ToFrozenSet();
        if (userId == Guid.Empty || institutionId == Guid.Empty || membershipId == Guid.Empty ||
            values.Count == 0 || values.Any(r => !Enum.IsDefined(r)))
            throw new ArgumentException("Contesto istituzionale non valido.");
        (UserId, InstitutionId, MembershipId, Roles) = (userId, institutionId, membershipId, values);
    }
}

public sealed record MembershipAuthorization(Guid UserId, Guid InstitutionId, Guid MembershipId,
    string InstitutionName, InstitutionStatus InstitutionStatus, MembershipStatus Status,
    IReadOnlyList<InstitutionRole> Roles);
