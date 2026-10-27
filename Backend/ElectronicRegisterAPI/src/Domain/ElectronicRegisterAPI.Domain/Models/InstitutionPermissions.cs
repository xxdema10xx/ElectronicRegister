using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Models;

public enum InstitutionPermission
{
    UserRead, UserCreate, UserUpdate, UserDelete,
    GradeRead, GradeCreate, GradeUpdate, GradeDelete,
    ExamRead, ExamCreate, ExamUpdate, ExamAssign, ExamPublish, ExamClose, ExamDelete, ExamCancel,
    SubmissionRead, SubmissionReview, SubmissionFinalize,
    MembershipManage, InstitutionManage, StudentTransfer, CatalogManage
}

/// <summary>
/// Role capabilities only: callers must also validate the current membership,
/// tenant ownership, profiles/teaching assignment and resource business rules.
/// No legacy/global role participates in this decision.
/// </summary>
public static class InstitutionPermissions
{
    public static bool Can(IEnumerable<InstitutionRole> roles, InstitutionPermission permission)
    {
        var set = roles.ToHashSet();
        if (!Enum.IsDefined(permission) || set.Count == 0 || set.Any(r => !Enum.IsDefined(r))) return false;
        if (set.Contains(InstitutionRole.InstitutionAdmin)) return true;
        // A mixed Staff/Teacher membership cannot recover a destructive capability
        // from the historical Teacher policy. Admin remains an explicit exception.
        if (set.Contains(InstitutionRole.Staff) && permission is
            InstitutionPermission.UserDelete or InstitutionPermission.GradeDelete or
            InstitutionPermission.ExamDelete or InstitutionPermission.ExamCancel) return false;
        if (set.Contains(InstitutionRole.Staff) && permission is
            InstitutionPermission.UserRead or InstitutionPermission.UserCreate or InstitutionPermission.UserUpdate or
            InstitutionPermission.GradeRead or InstitutionPermission.GradeCreate or InstitutionPermission.GradeUpdate or
            InstitutionPermission.ExamRead or InstitutionPermission.ExamCreate or InstitutionPermission.ExamUpdate or
            InstitutionPermission.ExamAssign or InstitutionPermission.ExamPublish or InstitutionPermission.ExamClose or
            InstitutionPermission.SubmissionRead) return true;
        return set.Contains(InstitutionRole.Teacher) && permission is
            InstitutionPermission.UserRead or InstitutionPermission.GradeRead or InstitutionPermission.GradeCreate or
            InstitutionPermission.GradeUpdate or InstitutionPermission.ExamRead or InstitutionPermission.ExamCreate or
            InstitutionPermission.ExamUpdate or InstitutionPermission.ExamAssign or InstitutionPermission.ExamPublish or
            InstitutionPermission.ExamClose or InstitutionPermission.ExamDelete or InstitutionPermission.ExamCancel or
            InstitutionPermission.SubmissionRead or InstitutionPermission.SubmissionReview or InstitutionPermission.SubmissionFinalize
            || set.Contains(InstitutionRole.Student) && permission == InstitutionPermission.GradeRead;
    }

    public static void Require(TenantContext tenant, InstitutionPermission permission)
    {
        if (!Can(tenant.Roles, permission)) throw new UnauthorizedAccessException("Operazione istituzionale non autorizzata.");
    }
}
