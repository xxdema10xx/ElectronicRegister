using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IExamService
{
    Task EnsureClassSubjectExistsAsync(Guid classSubjectId);
    Task EnsureTeacherTeachesClassSubjectAsync(Guid teacherId, Guid classSubjectId);
    Task EnsureClassSubjectBelongsToBienniumStudyPathAsync(Guid classSubjectId);
}