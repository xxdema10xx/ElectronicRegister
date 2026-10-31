using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IClassSubjectRepository
{
    Task<ClassSubject?> GetByIdAsync(Guid id);
    Task<List<ClassSubject>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<ClassSubject?> GetAsync(Guid classId, Guid subjectId, Guid? teacherId = null);
}
