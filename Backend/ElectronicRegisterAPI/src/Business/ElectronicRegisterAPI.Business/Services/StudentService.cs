using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class StudentService : IStudentService
{ 
    private readonly IStudentRepository _studentRepository;
    private readonly IGradeRepository _gradeRepository;

    public StudentService(IStudentRepository studentRepository, IGradeRepository gradeRepository)
    {
        _studentRepository = studentRepository;
        _gradeRepository = gradeRepository;
    }

    public async Task EnsureStudentCanBeDeletedAsync(Guid studentId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student is null)
            throw new KeyNotFoundException("Lo studente non esiste.");
        var hasGrades = await _gradeRepository.ExistsForStudentAsync(studentId);
        if (hasGrades)
            throw new BusinessRuleException("Lo studente ha voti registrati e non può essere eliminato.");
    }
}