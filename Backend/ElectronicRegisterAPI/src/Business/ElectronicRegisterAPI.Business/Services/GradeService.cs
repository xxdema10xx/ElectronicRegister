using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

internal class GradeService : IGradeService
{
    private readonly IGradeRepository _gradeRepository;
    private readonly IClassSubjectRepository _classSubjectRepository;

    private readonly decimal minGradeValue = 1;
    private readonly decimal maxGradeValue = 10;

    public GradeService(
        IGradeRepository gradeRepository,
        IClassSubjectRepository classSubjectRepository)
    {
        _gradeRepository = gradeRepository;
        _classSubjectRepository = classSubjectRepository;
    }

    public async Task EnsureGradeExists(Guid id)
    {
        var grade = await _gradeRepository.GetByIdAsync(id);
        if (grade is null)
            throw new BusinessRuleException("Il voto specificato non esiste.");
    }

    public void EnsureValidGradeValue(decimal value)
    {
        if (value < minGradeValue || value > maxGradeValue)
            throw new ArgumentOutOfRangeException(nameof(value), $"Il valore del voto deve essere compreso tra {minGradeValue} e {maxGradeValue}.");
    }

    public async Task EnsureTeacherTeachesClassSubjectAsync(Guid teacherId, Guid classSubjectId)
    {
        var classSubject =await _classSubjectRepository.GetByIdAsync(classSubjectId);

        if (classSubject is null || classSubject.TeacherId != teacherId)
        {
            throw new UnauthorizedAccessException(
                "Il docente non insegna questa materia nella classe specificata.");
        }
    }

    public async Task EnsureTeacherOwnsGradeAsync(Guid teacherId, Grade grade)
    {
        var classSubject = await _classSubjectRepository.GetByIdAsync(grade.ClassSubjectId);

        if (classSubject is null || classSubject.TeacherId != teacherId)
        {
            throw new UnauthorizedAccessException(
                "Il docente non è autorizzato a modificare questo voto.");
        }
    }

    public async Task EnsureStudentBelongsToClassSubjectAsync(Student student, ClassSubject classSubject)
    {
        if (!student.ClassId.HasValue || student.ClassId.Value != classSubject.ClassId)
        {
            throw new BusinessRuleException(
                "Lo studente non appartiene alla classe della materia specificata.");
        }
    }
}