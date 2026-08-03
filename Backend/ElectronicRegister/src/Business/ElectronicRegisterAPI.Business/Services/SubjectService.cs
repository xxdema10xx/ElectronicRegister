using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class SubjectService : ISubjectService
{
    private readonly ISubjectRepository _subjectRepository;

    public SubjectService(ISubjectRepository subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public async Task EnsureSubjectCanBeDeletedAsync(Guid subjectId)
    {
        var subject = await _subjectRepository.GetByIdAsync(subjectId);
        if (subject is null)
            throw new KeyNotFoundException("La materia non esiste.");
        if (subject.Grades.Any())
            throw new InvalidOperationException("La materia ha voti registrati e non può essere eliminata.");
    }

    public async Task EnsureSubjectExistsAsync(Guid subjectId)
    {
        var subject = await _subjectRepository.GetByIdAsync(subjectId);
        if (subject is null)
            throw new KeyNotFoundException("La materia non esiste.");
    }
}