using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
namespace ElectronicRegisterAPI.Application.Services;
// Used by the MT-3B institutional managers. Legacy managers are NOT bridged through it.
internal sealed class InstitutionProfileResolver(IInstitutionAuthManager authorization, IInstitutionProfileRepository profiles) : IInstitutionProfileResolver
{
    public async Task<Guid> RequireStudentAsync(TenantContext tenant, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(tenant, InstitutionRole.Student, cancellationToken);
        return await profiles.GetStudentAsync(tenant, cancellationToken) ?? throw new UnauthorizedAccessException("Profilo istituzionale non disponibile.");
    }
    public async Task<Guid> RequireTeacherAsync(TenantContext tenant, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(tenant, InstitutionRole.Teacher, cancellationToken);
        return await profiles.GetTeacherAsync(tenant, cancellationToken) ?? throw new UnauthorizedAccessException("Profilo istituzionale non disponibile.");
    }
    private async Task ValidateAsync(TenantContext tenant, InstitutionRole role, CancellationToken ct)
    {
        if (!tenant.Roles.Contains(role) || await authorization.AuthorizeAsync(tenant, ct) is null)
            throw new UnauthorizedAccessException("Profilo istituzionale non autorizzato.");
    }
}
