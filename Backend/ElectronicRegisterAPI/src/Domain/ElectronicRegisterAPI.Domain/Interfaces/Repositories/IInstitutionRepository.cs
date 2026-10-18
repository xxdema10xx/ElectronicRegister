using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

// Preparatory persistence only. No activation API and no legacy authorization integration in MT-1.
public interface IInstitutionRepository
{
    Task<Institution?> GetByIdAsync(Guid id);
    Task<Institution?> GetByCodeAsync(string code);
    Task AddAsync(Institution institution);
}
