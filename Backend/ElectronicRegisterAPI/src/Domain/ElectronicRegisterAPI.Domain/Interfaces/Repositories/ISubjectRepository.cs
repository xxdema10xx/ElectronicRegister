using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface ISubjectRepository
{
    Task<List<Subject>> GetByIdsAsync(IEnumerable<Guid> ids);
}
