using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

// Context comes exclusively from the existing scoped ICurrentTenant, never a DTO.
public interface IInstitutionStudentManager
{
    Task<List<StudentDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<StudentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<StudentDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<List<StudentDto>> GetByLastNameAsync(string name, CancellationToken ct = default);
    Task<StudentDto?> GetOwnProfileAsync(CancellationToken ct = default);
    Task<Guid> AddAsync(CreateStudentDto dto, Guid? classId = null, Guid? membershipId = null, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateStudentDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IInstitutionTeacherManager
{
    Task<List<TeacherDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<TeacherDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TeacherDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<List<TeacherDto>> GetByLastNameAsync(string name, CancellationToken ct = default);
    Task<List<TeacherDto>> GetForClassAsync(Guid classId, CancellationToken ct = default);
    Task<TeacherDto?> GetOwnProfileAsync(CancellationToken ct = default);
    Task<Guid> AddAsync(CreateTeacherDto dto, Guid? membershipId = null, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateTeacherDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IInstitutionSubjectManager
{
    Task<List<SubjectDto>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<SubjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<SubjectDto>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<SubjectDto?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<List<SubjectDto>> GetByTeacherAsync(Guid teacherId, CancellationToken ct = default);
    Task<Guid> AddAsync(CreateSubjectDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateSubjectDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
