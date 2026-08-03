using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class StudentService : IStudentService
{ 
    private readonly IStudentRepository _studentRepository;

    public StudentService(IStudentRepository studentRepository)
    {
        _studentRepository = studentRepository;
    }

    public async Task EnsureStudentCanBeDeletedAsync(Guid studentId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student is null)
            throw new KeyNotFoundException("Lo studente non esiste.");
        if (student.Grades.Any())
            throw new InvalidOperationException("Lo studente ha voti registrati e non può essere eliminato.");
    }

    public async Task EnsureStudentExistsAsync(Guid studentId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student is null)
            throw new KeyNotFoundException("Lo studente non esiste.");
    }
}