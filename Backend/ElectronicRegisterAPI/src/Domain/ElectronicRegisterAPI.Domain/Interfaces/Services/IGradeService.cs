using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IGradeService
{
    void EnsureValidGradeValue(decimal value);
    Task EnsureTeacherTeachesClassSubjectAsync(Guid teacherId, Guid classSubjectId);
    Task EnsureTeacherOwnsGradeAsync(Guid teacherId, Grade grade);
    Task EnsureStudentBelongsToClassSubjectAsync(Student student, ClassSubject classSubject);
}
