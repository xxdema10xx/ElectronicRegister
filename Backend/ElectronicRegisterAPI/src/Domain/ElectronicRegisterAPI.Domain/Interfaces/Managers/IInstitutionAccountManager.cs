using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IInstitutionAccountManager
{
    Task<InstitutionAccountCreated> CreateAsync(CreateInstitutionAccount request, CancellationToken ct = default);
}
