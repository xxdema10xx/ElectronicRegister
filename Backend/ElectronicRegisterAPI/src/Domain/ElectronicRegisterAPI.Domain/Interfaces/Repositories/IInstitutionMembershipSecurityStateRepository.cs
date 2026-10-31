namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

// Persistence preparation only. Not an alternative tenant authorization mechanism.
public interface IInstitutionMembershipSecurityStateRepository
{
    Task<long> GetRequiredGenerationAsync(Guid institutionId, Guid membershipId, CancellationToken ct = default);
}
