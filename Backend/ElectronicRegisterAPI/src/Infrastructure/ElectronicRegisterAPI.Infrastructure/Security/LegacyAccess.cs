using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal sealed class LegacyAccess(IOptions<LegacyIdentityOptions> options, IUserRepository users,
    IMembershipAuthorizationRepository memberships, IInstitutionRepository institutions) : ILegacyAccess
{
    public Task<bool> IsAllowedAsync(Guid userId, CancellationToken cancellationToken = default) => AllowedAsync(userId, null, cancellationToken);
    public Task<bool> IsTokenAllowedAsync(ClaimsContext caller, CancellationToken cancellationToken = default) => AllowedAsync(caller.UserId, caller, cancellationToken);

    private async Task<bool> AllowedAsync(Guid userId, ClaimsContext? token, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled || config.InstitutionId == Guid.Empty || !config.UserIds.Contains(userId)) return false;
        var user = await users.GetByIdAsync(userId);
        if (user is null) return false;
        if (token is not null && (token.Role != user.Role || token.StudentId != user.StudentId || token.TeacherId != user.TeacherId)) return false;
        var rows = await memberships.GetForUserAsync(userId, cancellationToken);
        // Before backfill: only explicit server-side identities. Never a fallback membership.
        // After backfill: legacy role is usable only inside this single migration holding.
        if (rows.Any(m => m.InstitutionId != config.InstitutionId)) return false;
        if (rows.Count == 0)
        {
            if (!config.AllowUnbackfilledAccounts) return false;
            var holding = await institutions.GetByIdAsync(config.InstitutionId);
            return holding is null || holding.Status == InstitutionStatus.Active;
        }
        var expectedRole = user.Role switch
        {
            UserRole.Admin => InstitutionRole.InstitutionAdmin,
            UserRole.Teacher => InstitutionRole.Teacher,
            UserRole.Student => InstitutionRole.Student,
            _ => (InstitutionRole?)null
        };
        return rows.All(m => m.Status == MembershipStatus.Active &&
            m.InstitutionStatus == InstitutionStatus.Active && expectedRole.HasValue && m.Roles.Contains(expectedRole.Value));
    }
}
