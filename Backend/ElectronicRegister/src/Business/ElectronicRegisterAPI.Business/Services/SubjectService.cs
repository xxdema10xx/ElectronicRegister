using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class SubjectService : ISubjectService
{
    private readonly ISubjectRepository _subjectRepository;
    private readonly IGradeRepository _gradeRepository;

    public SubjectService(ISubjectRepository subjectRepository, IGradeRepository gradeRepository)
    {
        _subjectRepository = subjectRepository;
        _gradeRepository = gradeRepository;
    }

    public async Task EnsureSubjectCanBeDeletedAsync(Guid subjectId)
    {
        var subject = await _subjectRepository.GetByIdAsync(subjectId);
        if (subject is null)
            throw new KeyNotFoundException("La materia non esiste.");
        var hasGrades = await _gradeRepository.ExistsForSubjectAsync(subjectId);
        if (hasGrades)
            throw new InvalidOperationException("La materia ha voti registrati e non può essere eliminata.");
    }

    public async Task EnsureNameIsAvailableAsync(string name)
    {
        var subject = await _subjectRepository.GetByNameAsync(name);
        if (subject != null)
            throw new InvalidOperationException("Il nome della materia è già in uso.");
    }

    public async Task EnsureSubjectExistsAsync(Guid subjectId)
    {
        var subject = await _subjectRepository.GetByIdAsync(subjectId);
        if (subject is null)
            throw new KeyNotFoundException("La materia non esiste.");
    }
}