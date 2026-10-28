using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Services;

internal sealed class InstitutionSchoolAuthorization(IInstitutionAuthManager authorization, IInstitutionProfileResolver profiles, ICurrentTenant? current = null)
{
    internal async Task<TenantContext> RequireAsync(CancellationToken ct)
    {
        // Non-HTTP callers without the existing scoped accessor fail closed.
        var tenant = current?.Value ?? throw new UnauthorizedAccessException("Contesto istituzionale assente.");
        if (await authorization.AuthorizeAsync(tenant, ct)is null)
            throw new UnauthorizedAccessException("Contesto istituzionale non autorizzato.");
        return tenant;
    }

    internal async Task<TenantContext> AdminAsync(CancellationToken ct)
    {
        var t = await RequireAsync(ct);
        if (!t.Roles.Contains(InstitutionRole.InstitutionAdmin))
            throw new UnauthorizedAccessException("Permesso amministrativo richiesto.");
        return t;
    }

    internal async Task<TenantContext> PermissionAsync(InstitutionPermission permission, CancellationToken ct)
    {
        var tenant = await RequireAsync(ct);
        InstitutionPermissions.Require(tenant, permission);
        return tenant;
    }

    internal async Task<TenantContext> ReadAsync(bool students, CancellationToken ct)
    {
        var t = await RequireAsync(ct);
        if (t.Roles.Contains(InstitutionRole.InstitutionAdmin) || t.Roles.Contains(InstitutionRole.Staff))
            return t;
        if (t.Roles.Contains(InstitutionRole.Teacher))
            await profiles.RequireTeacherAsync(t, ct);
        else if (!students && t.Roles.Contains(InstitutionRole.Student))
            await profiles.RequireStudentAsync(t, ct);
        else
            throw new UnauthorizedAccessException("Anagrafica non autorizzata.");
        return t;
    }

    internal async Task<(TenantContext Tenant, Guid Id)> OwnAsync(bool teacher, CancellationToken ct)
    {
        var t = await RequireAsync(ct);
        return (t, teacher ? await profiles.RequireTeacherAsync(t, ct) : await profiles.RequireStudentAsync(t, ct));
    }

    internal static string Name(string? value, int max = 100) => !string.IsNullOrWhiteSpace(value) && value.Length <= max ? value : throw new ArgumentException("Nome non valido.");
}
