using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;
public interface IInstitutionClassSubjectManager
{
    Task<InstitutionAcademicPage<InstitutionClassSubjectDto>> ListAsync(int page, int size, Guid? classId, Guid? subjectId, Guid? teacherId, CancellationToken ct);
    Task<InstitutionClassSubjectDto?> GetAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateAsync(CreateInstitutionClassSubjectDto dto, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdateInstitutionClassSubjectDto dto, long revision, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
