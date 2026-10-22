using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;
public interface IInstitutionClassSubjectRepository
{
    Task<InstitutionAcademicPage<InstitutionClassSubjectDto>> ListAsync(TenantContext t, int page, int size, Guid? classId, Guid? subjectId, Guid? teacherId, CancellationToken ct);
    Task<InstitutionClassSubjectDto?> GetAsync(TenantContext t, Guid id, CancellationToken ct);
    Task<Guid> CreateAsync(TenantContext t, CreateInstitutionClassSubjectDto dto, CancellationToken ct);
    Task UpdateAsync(TenantContext t, Guid id, UpdateInstitutionClassSubjectDto dto, long revision, CancellationToken ct);
    Task DeleteAsync(TenantContext t, Guid id, CancellationToken ct);
}
