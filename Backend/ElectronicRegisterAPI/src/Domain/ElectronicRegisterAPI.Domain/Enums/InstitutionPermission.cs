using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

public enum InstitutionPermission
{
    UserRead,
    UserCreate,
    UserUpdate,
    UserDelete,
    GradeRead,
    GradeCreate,
    GradeUpdate,
    GradeDelete,
    ExamRead,
    ExamCreate,
    ExamUpdate,
    ExamAssign,
    ExamPublish,
    ExamClose,
    ExamDelete,
    ExamCancel,
    SubmissionRead,
    SubmissionReview,
    SubmissionFinalize,
    MembershipManage,
    InstitutionManage,
    StudentTransfer,
    CatalogManage
}
