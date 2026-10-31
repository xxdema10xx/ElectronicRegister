using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

public class SubjectService(IClassSubjectRepository classSubjectRepository) : ISubjectService
{
    private readonly IClassSubjectRepository _classSubjectRepository = classSubjectRepository;

    public async Task<ClassSubject> EnsureValidClassSubjectId(Guid classSubjectId)
    {
        if (classSubjectId == Guid.Empty)
            throw new ArgumentException("L'identificativo della materia associata alla classe non può essere vuoto.");
        var classSubject = await _classSubjectRepository.GetByIdAsync(classSubjectId);
        if (classSubject is null)
            throw new KeyNotFoundException("La materia associata alla classe non esiste.");
        return classSubject;
    }

}
