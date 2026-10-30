using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

/// <summary>An operation view preserves the complete verified membership role set.</summary>
public sealed class ExamPrincipal
{
    public TenantContext Tenant { get; }
    public bool StudentView { get; }
    public bool CombinedRead { get; }
    public Guid UserId => Tenant.UserId;
    public Guid? StudentId { get; }
    public Guid? TeacherId { get; }
    public bool IsAdmin => !StudentView && Tenant.Roles.Contains(InstitutionRole.InstitutionAdmin);
    public bool IsStaff => Tenant is not null && !StudentView && TeacherId is null && !IsAdmin && Tenant.Roles.Contains(InstitutionRole.Staff);
    public bool HasInstitutionReadScope => IsAdmin || IsStaff;
    public bool IsTeacher => !StudentView && !IsAdmin && !IsStaff && Tenant.Roles.Contains(InstitutionRole.Teacher);
    public bool IsStudent => StudentView && Tenant.Roles.Contains(InstitutionRole.Student);
    public bool CanDeleteExam => !StudentView && InstitutionPermissions.Can(Tenant.Roles, InstitutionPermission.ExamDelete);
    public bool CanCancelExam => !StudentView && InstitutionPermissions.Can(Tenant.Roles, InstitutionPermission.ExamCancel);

    public bool Can(InstitutionPermission permission) => !StudentView && InstitutionPermissions.Can(Tenant.Roles, permission);
    public ExamPrincipal(
        TenantContext tenant,
        bool studentView,
        Guid? studentId,
        Guid? teacherId,
        bool combinedRead = false
    )
    {
        Tenant = tenant;
        StudentView = studentView;
        StudentId = studentId;
        TeacherId = teacherId;
        CombinedRead = combinedRead;
        if (studentView ? !IsStudent || studentId is null : !IsAdmin && !IsStaff && (!IsTeacher || teacherId is null))
            throw new UnauthorizedAccessException("Profilo istituzionale non autorizzato.");
    }

}
