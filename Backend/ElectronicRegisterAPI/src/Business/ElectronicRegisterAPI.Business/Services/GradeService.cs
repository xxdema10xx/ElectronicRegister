using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

public class GradeService : IGradeService
{
    private readonly IClassSubjectRepository _classSubjectRepository;
    public GradeService(IClassSubjectRepository classSubjectRepository)
    {
        _classSubjectRepository = classSubjectRepository;
    }

    public void EnsureValidGradeValue(decimal value) => GradeRules.EnsureValidValue(value);
    public async Task EnsureTeacherTeachesClassSubjectAsync(Guid teacherId, Guid classSubjectId)
    {
        var classSubject = await _classSubjectRepository.GetByIdAsync(classSubjectId);
        if (classSubject is null || classSubject.TeacherId != teacherId)
        {
            throw new UnauthorizedAccessException("Il docente non insegna questa materia nella classe specificata.");
        }
    }

    public async Task EnsureTeacherOwnsGradeAsync(Guid teacherId, Grade grade)
    {
        var classSubject = await _classSubjectRepository.GetByIdAsync(grade.ClassSubjectId);
        if (classSubject is null || classSubject.TeacherId != teacherId)
        {
            throw new UnauthorizedAccessException("Il docente non è autorizzato a modificare questo voto.");
        }
    }

    public async Task EnsureStudentBelongsToClassSubjectAsync(Student student, ClassSubject classSubject)
    {
        if (!student.ClassId.HasValue || student.ClassId.Value != classSubject.ClassId)
        {
            throw new BusinessRuleException("Lo studente non appartiene alla classe della materia specificata.");
        }
    }
}
