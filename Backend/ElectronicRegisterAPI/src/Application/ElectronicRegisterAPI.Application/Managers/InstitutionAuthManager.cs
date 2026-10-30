using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionAuthManager(
    IPasswordHasher passwords,
    IMembershipAuthorizationRepository memberships,
    IInstitutionTokens tokens,
    IMicrosoftIdentityLink microsoftLinks,
    IIdentityAccountRepository accounts
) : IInstitutionAuthManager
{
    public async Task<IdentityTicketDto?> LoginAsync(LoginDto input)
    {
        var account = await accounts.FindByEmailAsync(input.Email);
        return account is not null && passwords.Verify(input.Password, account.PasswordHash) ? new(tokens.Selection(account.UserId, account.Generation), 300) : null;
    }

    public async Task<IdentityTicketDto?> MicrosoftLoginAsync(MicrosoftLoginDto input)
    {
        var userId = await microsoftLinks.AuthenticateAsync(input.AccessToken);
        var account = userId.HasValue ? await accounts.GetAsync(userId.Value) : null;
        return account is not null ? new(tokens.Selection(account.UserId, account.Generation), 300) : null;
    }

    public async Task<bool> IdentityExistsAsync(Guid userId) => await accounts.GenerationAsync(userId) >= 0;
    public async Task<IReadOnlyList<InstitutionChoiceDto>> InstitutionsAsync(Guid userId, CancellationToken cancellationToken) => (await memberships.GetForUserAsync(userId, cancellationToken)).Where(MembershipAuthorizationRules.IsSelectable).Select(m => new InstitutionChoiceDto(m.InstitutionId, m.MembershipId, m.InstitutionName, m.Roles.Select(r => r.ToString()).Order().ToArray())).ToArray();
    public async Task<InstitutionAccessDto> SelectAsync(
        Guid userId,
        SelectInstitutionDto input,
        CancellationToken cancellationToken,
        long identityGeneration = 0
    )
    {
        if (await accounts.GenerationAsync(userId, cancellationToken) != identityGeneration)
            throw new UnauthorizedAccessException("Sessione revocata.");
        var membership = await memberships.GetAsync(userId, input.InstitutionId, input.MembershipId, cancellationToken);
        if (membership is null || !MembershipAuthorizationRules.IsSelectable(membership))
            throw new UnauthorizedAccessException("Contesto non autorizzato.");
        if (input.RequestedRole is not null && (!Enum.TryParse<InstitutionRole>(input.RequestedRole, out var role) || !Enum.IsDefined(role) || role.ToString() != input.RequestedRole || !membership.Roles.Contains(role)))
            throw new UnauthorizedAccessException("Contesto non autorizzato.");
        return new(tokens.Access(new(userId, membership.InstitutionId, membership.MembershipId, membership.Roles, membership.SecurityGeneration), identityGeneration), 900);
    }

    public async Task<TenantContext?> AuthorizeAsync(TenantContext token, CancellationToken cancellationToken)
    {
        var current = await memberships.GetAsync(token.UserId, token.InstitutionId, token.MembershipId, cancellationToken);
        return current is not null && MembershipAuthorizationRules.Matches(token, current) ? token : null;
    }
}
