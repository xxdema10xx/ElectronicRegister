using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IBienniumStudyPathRepository
{
    Task<Dictionary<Guid, StudyPath>> GetStudyPathsByIdsAsync(IEnumerable<Guid> bienniumStudyPathIds);
}
