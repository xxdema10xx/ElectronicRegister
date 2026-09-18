using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class TeacherService : ITeacherService
{
    private readonly IClassSubjectRepository _classSubjectRepository;

    public TeacherService(IClassSubjectRepository classSubjectRepository)
    {
        _classSubjectRepository = classSubjectRepository;
    }

    public async Task EnsureTeacherCanBeDeletedAsync(Guid teacherId)
    {
        if (await _classSubjectRepository.ExistsForTeacherAsync(teacherId))
            throw new BusinessRuleException("Impossibile eliminare l'insegnante: ha assegnazioni a classi e materie.");
    }
}