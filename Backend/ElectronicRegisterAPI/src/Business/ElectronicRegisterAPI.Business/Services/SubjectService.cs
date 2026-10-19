using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

internal class SubjectService : ISubjectService
{
    private readonly ISubjectRepository _subjectRepository;
    private readonly IClassSubjectRepository _classSubjectRepository;

    public SubjectService(ISubjectRepository subjectRepository, IClassSubjectRepository classSubjectRepository)
    {
        _subjectRepository = subjectRepository;
        _classSubjectRepository = classSubjectRepository;
    }

    public void EnsureClassSubjectExists(ClassSubject? classSubject)
    {
        if (classSubject is null) throw new KeyNotFoundException("La materia associata alla classe non esiste.");
    }

    public async Task<ClassSubject> EnsureValidClassSubjectId(Guid classSubjectId)
    {
        if (classSubjectId == Guid.Empty) throw new ArgumentException("L'identificativo della materia associata alla classe non può essere vuoto.");
        var classSubject = await _classSubjectRepository.GetByIdAsync(classSubjectId);
        if ( classSubject is null) throw new KeyNotFoundException("La materia associata alla classe non esiste.");
        return classSubject;
    }

    public async Task EnsureSubjectCanBeDeletedAsync(Guid subjectId)
    {
        var subject = await _subjectRepository.GetByIdAsync(subjectId);

        if (subject is null) throw new KeyNotFoundException("La materia non esiste.");

        var hasAssignments = await _classSubjectRepository.ExistsForSubjectAsync(subjectId);

        if (hasAssignments) throw new BusinessRuleException("La materia è assegnata a una o più classi e non può essere eliminata.");
    }

    public async Task EnsureNameIsAvailableAsync(string name)
    {
        var subject = await _subjectRepository.GetByNameAsync(name);
        if (subject != null) throw new BusinessRuleException("Il nome della materia è già in uso.");
    }

    public async Task EnsureSubjectExistsAsync(Guid subjectId)
    {
        var subject = await _subjectRepository.GetByIdAsync(subjectId);
        if (subject is null) throw new KeyNotFoundException("La materia non esiste.");
    }
}