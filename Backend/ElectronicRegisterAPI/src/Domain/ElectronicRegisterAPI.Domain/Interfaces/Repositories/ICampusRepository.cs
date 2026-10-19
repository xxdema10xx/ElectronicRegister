using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ICampusRepository
{
    Task<Campus?> GetByIdAsync(Guid institutionId, Guid id);
    Task<List<Campus>> GetByInstitutionAsync(Guid institutionId);
    Task AddAsync(Campus campus);
}
