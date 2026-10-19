using ElectronicRegisterAPI.Domain.Models;
namespace ElectronicRegisterAPI.Domain.Interfaces.Services;
public interface IInstitutionProfileResolver
{
    Task<Guid> RequireStudentAsync(TenantContext tenant, CancellationToken cancellationToken = default);
    Task<Guid> RequireTeacherAsync(TenantContext tenant, CancellationToken cancellationToken = default);
}
