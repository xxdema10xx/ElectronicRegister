using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionAccountManager(
    InstitutionSchoolAuthorization access,
    IInstitutionAccountRepository accounts,
    IPasswordHasher passwords,
    IFiscalCodeProtector fiscalCodes,
    PlatformIdentitySettings platform
) : IInstitutionAccountManager
{
    public async Task<InstitutionAccountCreated> CreateAsync(CreateInstitutionAccount request, CancellationToken ct = default)
    {
        platform.RequireEnabled();
        // Institution, membership and roles come from the verified token; the repository
        // re-checks them under the IAM locks inside the same transaction.
        var actor = await access.PermissionAsync(InstitutionPermission.UserCreate, ct);
        InstitutionAccountRules.Validate(request);
        InstitutionAccountRules.RequireAllowed(actor, request.Roles);
        var normalized = request with
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            FiscalCode = FiscalCodeRules.Normalize(request.FiscalCode),
            Reason = request.Reason.Trim()
        };
        var localBase = PlatformEmailRules.Base(normalized.FirstName, normalized.LastName) ?? throw new ArgumentException("Nome e cognome devono contenere lettere rappresentabili in un indirizzo email.");
        var fiscalCode = fiscalCodes.Protect(normalized.FiscalCode);
        // The hash is computed before any database access, so every branch (new person,
        // existing person, reactivation, unavailable) costs the same BCrypt work.
        var temporary = TemporaryPasswords.New();
        return await accounts.CreateAsync(actor, normalized with { FiscalCode = "" }, fiscalCode, localBase, passwords.Hash(temporary), temporary, ct);
    }
}
