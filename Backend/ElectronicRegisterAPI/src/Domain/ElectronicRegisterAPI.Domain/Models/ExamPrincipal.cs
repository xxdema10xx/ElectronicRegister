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
    public bool IsTeacher => Tenant is null ? _legacy!.Role == UserRole.Teacher : !StudentView && !IsAdmin && Tenant.Roles.Contains(InstitutionRole.Teacher);
    public bool IsStudent => Tenant is null ? _legacy!.Role == UserRole.Student : StudentView && Tenant.Roles.Contains(InstitutionRole.Student);
    private ExamPrincipal(ClaimsContext legacy) { _legacy = legacy; StudentId = legacy.StudentId; TeacherId = legacy.TeacherId; }
    public ExamPrincipal(TenantContext tenant, bool studentView, Guid? studentId, Guid? teacherId, bool combinedRead = false)
    {
        Tenant = tenant; StudentView = studentView; StudentId = studentId; TeacherId = teacherId; CombinedRead = combinedRead;
        if (studentView ? !IsStudent || studentId is null : !IsAdmin && (!IsTeacher || teacherId is null))
            throw new UnauthorizedAccessException("Profilo istituzionale non autorizzato.");
    }
    public static ExamPrincipal Legacy(ClaimsContext caller) => new(caller ?? throw new ArgumentNullException(nameof(caller)));
    // Compatibility only for existing DTO redaction, never a source of institutional authorization.
    public ClaimsContext VisibilityCaller => new(UserId, IsStudent ? UserRole.Student : IsAdmin ? UserRole.Admin : UserRole.Teacher, StudentId, TeacherId);
}
