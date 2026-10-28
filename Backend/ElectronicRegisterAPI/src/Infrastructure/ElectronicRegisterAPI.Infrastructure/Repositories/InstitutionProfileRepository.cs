using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionProfileRepository(ElectronicRegisterContext db) : IInstitutionProfileRepository
{
    public Task<Guid?> GetStudentAsync(TenantContext tenant, CancellationToken cancellationToken = default) => db.InstitutionStudentProfiles.AsNoTracking().Where(p => p.InstitutionId == tenant.InstitutionId && p.MembershipId == tenant.MembershipId && p.InstitutionMembership.UserId == tenant.UserId && EF.Property<Guid>(p.Student, "InstitutionId") == tenant.InstitutionId).Select(p => (Guid? )p.StudentId).SingleOrDefaultAsync(cancellationToken);
    public Task<Guid?> GetTeacherAsync(TenantContext tenant, CancellationToken cancellationToken = default) => db.InstitutionTeacherProfiles.AsNoTracking().Where(p => p.InstitutionId == tenant.InstitutionId && p.MembershipId == tenant.MembershipId && p.InstitutionMembership.UserId == tenant.UserId && EF.Property<Guid>(p.Teacher, "InstitutionId") == tenant.InstitutionId).Select(p => (Guid? )p.TeacherId).SingleOrDefaultAsync(cancellationToken);
}
