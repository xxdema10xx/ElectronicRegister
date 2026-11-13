using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionAccountRepository
{
    Task<InstitutionAccountCreated> CreateAsync(
        TenantContext actor,
        CreateInstitutionAccount request,
        ProtectedFiscalCode fiscalCode,
        string localBase,
        string passwordHash,
        string temporaryPassword,
        CancellationToken ct = default
    );
}
