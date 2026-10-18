using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IMembershipAuthorizationRepository
{
    Task<IReadOnlyList<MembershipAuthorization>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MembershipAuthorization?> GetAsync(Guid userId, Guid institutionId, Guid membershipId, CancellationToken cancellationToken = default);
}
