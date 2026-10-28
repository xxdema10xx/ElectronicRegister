using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

/// <summary>An operation view preserves the complete verified membership role set.</summary>
public sealed class ExamPrincipal
{
    private readonly ClaimsContext? _legacy;
    public TenantContext? Tenant { get; }
    public bool StudentView { get; }
    public bool CombinedRead { get; }
    public Guid UserId => Tenant?.UserId ?? _legacy!.UserId;
    public Guid? StudentId { get; }
    public Guid? TeacherId { get; }
    public bool IsAdmin => Tenant is null ? _legacy!.Role == UserRole.Admin : !StudentView && Tenant.Roles.Contains(InstitutionRole.InstitutionAdmin);
    public bool IsStaff => Tenant is not null && !StudentView && TeacherId is null && !IsAdmin && Tenant.Roles.Contains(InstitutionRole.Staff);
    public bool HasInstitutionReadScope => IsAdmin || IsStaff;
    public bool IsTeacher => Tenant is null ? _legacy!.Role == UserRole.Teacher : !StudentView && !IsAdmin && !IsStaff && Tenant.Roles.Contains(InstitutionRole.Teacher);
    public bool IsStudent => Tenant is null ? _legacy!.Role == UserRole.Student : StudentView && Tenant.Roles.Contains(InstitutionRole.Student);
    public bool CanDeleteExam => Tenant is null ? IsAdmin || IsTeacher : !StudentView && InstitutionPermissions.Can(Tenant.Roles, InstitutionPermission.ExamDelete);
    public bool CanCancelExam => Tenant is null ? IsAdmin || IsTeacher : !StudentView && InstitutionPermissions.Can(Tenant.Roles, InstitutionPermission.ExamCancel);

    public bool Can(InstitutionPermission permission) => Tenant is null ? InstitutionPermissions.Can([IsAdmin ? InstitutionRole.InstitutionAdmin : IsTeacher ? InstitutionRole.Teacher : InstitutionRole.Student], permission) : !StudentView && InstitutionPermissions.Can(Tenant.Roles, permission);
    private ExamPrincipal(ClaimsContext legacy)
    {
        _legacy = legacy;
        StudentId = legacy.StudentId;
        TeacherId = legacy.TeacherId;
    }

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

    public static ExamPrincipal Legacy(ClaimsContext caller) => new(caller ?? throw new ArgumentNullException(nameof(caller)));
    // Compatibility only for existing DTO redaction, never a source of institutional authorization.
    public ClaimsContext VisibilityCaller => IsStaff ? throw new InvalidOperationException("Staff non ha un ruolo legacy equivalente.") : new(UserId, IsStudent ? UserRole.Student : IsAdmin ? UserRole.Admin : UserRole.Teacher, StudentId, TeacherId);
}
