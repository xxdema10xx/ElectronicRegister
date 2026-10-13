using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class ExamService : IExamService
{
    private readonly IClassSubjectRepository _classSubjectRepository;
    private readonly IItsClassRepository _itsClassRepository;
    private readonly IBienniumStudyPathRepository _bienniumStudyPathRepository;

    public ExamService(
        IClassSubjectRepository classSubjectRepository,
        IItsClassRepository itsClassRepository,
        IBienniumStudyPathRepository bienniumStudyPathRepository)
    {
        _classSubjectRepository = classSubjectRepository;
        _itsClassRepository = itsClassRepository;
        _bienniumStudyPathRepository = bienniumStudyPathRepository;
    }

    public async Task EnsureClassSubjectExistsAsync(Guid classSubjectId)
    {
        var classSubject = await _classSubjectRepository.GetByIdAsync(classSubjectId);

        if (classSubject is null)
            throw new BusinessRuleException("L'associazione classe-materia specificata non esiste.");
    }

    public async Task EnsureTeacherTeachesClassSubjectAsync(Guid teacherId, Guid classSubjectId)
    {
        var classSubject = await _classSubjectRepository.GetByIdAsync(classSubjectId);

        if (classSubject is null || classSubject.TeacherId != teacherId)
        {
            throw new UnauthorizedAccessException(
                "Il docente non insegna questa materia nella classe specificata.");
        }
    }

    public async Task EnsureClassSubjectBelongsToBienniumStudyPathAsync(Guid classSubjectId)
    {
        var classSubject = await _classSubjectRepository.GetByIdAsync(classSubjectId);

        if (classSubject is null)
            throw new BusinessRuleException("L'associazione classe-materia specificata non esiste.");

        var itsClass = await _itsClassRepository.GetByIdAsync(classSubject.ClassId);

        if (itsClass is null)
            throw new BusinessRuleException("La classe specificata non esiste.");

        var studyPaths = await _bienniumStudyPathRepository
            .GetStudyPathsByIdsAsync(new[] { itsClass.BienniumStudyPathId });

        if (!studyPaths.ContainsKey(itsClass.BienniumStudyPathId))
            throw new BusinessRuleException("La classe non appartiene a nessun corso del biennio.");
    }
}