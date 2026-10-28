using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IInstitutionProfileRepository
{
    Task<Guid?> GetStudentAsync(TenantContext tenant, CancellationToken cancellationToken = default);
    Task<Guid?> GetTeacherAsync(TenantContext tenant, CancellationToken cancellationToken = default);
}
