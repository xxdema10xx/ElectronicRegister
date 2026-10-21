using System.Collections.Frozen;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

// No legacy school profile is implied by a membership.
public sealed class TenantContext
{
    public Guid UserId { get; }
    public Guid InstitutionId { get; }
    public Guid MembershipId { get; }
    public long? SecurityGeneration { get; }
    public IReadOnlySet<InstitutionRole> Roles { get; }
    public TenantContext(Guid userId, Guid institutionId, Guid membershipId, IEnumerable<InstitutionRole> roles,
        long? securityGeneration = null)
    {
        var values = roles.ToFrozenSet();
        if (userId == Guid.Empty || institutionId == Guid.Empty || membershipId == Guid.Empty ||
            values.Count == 0 || values.Any(r => !Enum.IsDefined(r)) || securityGeneration < 0)
            throw new ArgumentException("Contesto istituzionale non valido.");
        (UserId, InstitutionId, MembershipId, Roles) = (userId, institutionId, membershipId, values);
        // Absence is never generation zero. Such a candidate cannot authorize or issue a JWT.
        SecurityGeneration = securityGeneration;
    }
}

public sealed record MembershipAuthorization(Guid UserId, Guid InstitutionId, Guid MembershipId,
    string InstitutionName, InstitutionStatus InstitutionStatus, MembershipStatus Status,
    IReadOnlyList<InstitutionRole> Roles, long? SecurityGeneration = null);
