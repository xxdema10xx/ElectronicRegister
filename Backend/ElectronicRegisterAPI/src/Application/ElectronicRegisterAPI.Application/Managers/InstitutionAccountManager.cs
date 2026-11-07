using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionAccountManager(InstitutionSchoolAuthorization access, IInstitutionAccountRepository accounts, IPasswordHasher passwords) : IInstitutionAccountManager
{
    public async Task<InstitutionAccountCreated> CreateAsync(CreateInstitutionAccount request, CancellationToken ct = default)
    {
        // Institution, membership and roles come from the verified token; the repository
        // re-checks them under the IAM locks inside the same transaction.
        var actor = await access.PermissionAsync(InstitutionPermission.UserCreate, ct);
        InstitutionAccountRules.Validate(request);
        InstitutionAccountRules.RequireAllowed(actor, request.Roles);
        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > 255 || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new ArgumentException("Email non valida.");
        // The hash is computed before any database access, so an unavailable address costs
        // the same BCrypt work as a successful creation.
        var temporary = TemporaryPasswords.New();
        return await accounts.CreateAsync(actor, request with { Email = email, Reason = request.Reason.Trim() }, passwords.Hash(temporary), temporary, ct);
    }
}
