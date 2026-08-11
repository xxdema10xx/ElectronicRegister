using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class TeacherService : ITeacherService
{
    private readonly ISubjectRepository _subjectRepository;
    private readonly IGradeRepository _gradeRepository;

    public TeacherService(ISubjectRepository subjectRepository, IGradeRepository gradeRepository)
    {
        _subjectRepository = subjectRepository;
        _gradeRepository = gradeRepository;
    }

    public async Task EnsureTeacherCanBeDeletedAsync(Guid teacherId)
    {
        if (await _subjectRepository.ExistsForTeacherAsync(teacherId))
            throw new BusinessRuleException("Impossibile eliminare l'insegnante: ha materie assegnate.");

        if (await _gradeRepository.ExistsForTeacherAsync(teacherId))
            throw new BusinessRuleException("Impossibile eliminare l'insegnante: ha voti assegnati.");
    }
}