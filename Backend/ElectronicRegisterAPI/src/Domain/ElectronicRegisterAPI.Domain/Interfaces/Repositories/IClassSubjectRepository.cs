using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IClassSubjectRepository
{
    Task<ClassSubject?> GetByIdAsync(Guid id);

    Task<List<ClassSubject>> GetByIdsAsync(IEnumerable<Guid> ids);

    Task<ClassSubject?> GetAsync(Guid classId, Guid subjectId, Guid? teacherId = null);

    Task<List<ClassSubject>> GetByTeacherIdAsync(Guid teacherId);

    Task<List<ClassSubject>> GetBySubjectIdAsync(Guid subjectId);

    Task<List<ClassSubject>> GetByClassIdAsync(Guid classId);

    Task<bool> ExistsForTeacherAsync(Guid teacherId);

    Task<bool> ExistsForSubjectAsync(Guid subjectId);
}