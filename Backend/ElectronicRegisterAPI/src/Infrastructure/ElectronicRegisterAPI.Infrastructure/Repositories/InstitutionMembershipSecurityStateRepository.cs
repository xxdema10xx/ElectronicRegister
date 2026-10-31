using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionMembershipSecurityStateRepository(ElectronicRegisterContext db) : IInstitutionMembershipSecurityStateRepository
{
    public async Task<long> GetRequiredGenerationAsync(Guid institutionId, Guid membershipId, CancellationToken ct = default)
    {
        if (institutionId == Guid.Empty || membershipId == Guid.Empty)
            throw new UnauthorizedAccessException();
        var generation = await db.InstitutionMembershipSecurityStates.AsNoTracking().Where(s => s.InstitutionId == institutionId && s.MembershipId == membershipId).Select(s => (long? )s.SecurityGeneration).SingleOrDefaultAsync(ct);
        return generation is >= 0 ? generation.Value : throw new UnauthorizedAccessException("Stato di sicurezza non disponibile.");
    }
}
