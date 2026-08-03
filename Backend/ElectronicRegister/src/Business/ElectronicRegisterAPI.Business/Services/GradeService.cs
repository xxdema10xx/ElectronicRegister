using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class GradeService : IGradeService
{
    private readonly ISubjectRepository _subjectRepository;

    private readonly decimal minGradeValue = 1;
    private readonly decimal maxGradeValue = 10;

    public GradeService(ISubjectRepository subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public void EnsureValidGradeValue(decimal value)
    {
        if (value < minGradeValue || value > maxGradeValue)
            throw new ArgumentOutOfRangeException(nameof(value), $"Il valore del voto deve essere compreso tra {minGradeValue} e {maxGradeValue}.");
    }

    public async Task EnsureTeacherTeachesSubjectAsync(Guid teacherId, Guid subjectId)
    {
        var subject = await _subjectRepository.GetByIdAsync(subjectId);
        if (subject is null || subject.TeacherId != teacherId)
            throw new UnauthorizedAccessException("Il docente non insegna questa materia.");
    }
}