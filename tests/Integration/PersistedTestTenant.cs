using System;
using System.Linq;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Tests.Integration;

// Direct service fixtures must supply the same persisted value as real token issuance.
// Never used by HTTP, token parsing or production; never refreshes an issued context.
internal static class PersistedTestTenant
{
    internal static async Task<TenantContext> ReadAsync(ElectronicRegisterContext db, TenantContext candidate)
    {
        if (candidate.SecurityGeneration.HasValue && candidate.IdentityGeneration.HasValue) return candidate;
        var generation = candidate.SecurityGeneration ?? await db.InstitutionMembershipSecurityStates.AsNoTracking()
            .Where(s => s.InstitutionId == candidate.InstitutionId && s.MembershipId == candidate.MembershipId)
            .Select(s => (long?)s.SecurityGeneration).SingleOrDefaultAsync();
        if (generation is not >= 0) throw new UnauthorizedAccessException("Synthetic context has no security state.");
        var identity = candidate.IdentityGeneration ?? await db.Database.SqlQuery<long?>($"SELECT COALESCE(s.generation,0) AS Value FROM `user` u LEFT JOIN identity_account_state s ON s.user_id=u.id WHERE u.id={candidate.UserId}").SingleOrDefaultAsync();
        if (identity is not >= 0) throw new UnauthorizedAccessException("Synthetic context has no identity state.");
        return new(candidate.UserId, candidate.InstitutionId, candidate.MembershipId, candidate.Roles, generation, identity);
    }
}
