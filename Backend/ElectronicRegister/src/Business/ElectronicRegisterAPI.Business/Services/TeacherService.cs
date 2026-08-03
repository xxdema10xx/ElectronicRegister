using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class TeacherService : ITeacherService
{
    private readonly ITeacherRepository _teacherRepository;

    public TeacherService(ITeacherRepository teacherRepository)
    {
        _teacherRepository = teacherRepository;
    }

    public async Task EnsureTeacherCanBeDeletedAsync(Guid teacherId)
    {
        var teacher = await _teacherRepository.GetByIdAsync(teacherId);
        if (teacher is null)
            throw new KeyNotFoundException("Il docente non esiste.");
        if (teacher.Subjects.Any())
            throw new InvalidOperationException("Il docente ha materie assegnate e non può essere eliminato.");
    }

    public async Task EnsureTeacherExistsAsync(Guid teacherId)
    {
        var teacher = await _teacherRepository.GetByIdAsync(teacherId);
        if (teacher is null)
            throw new KeyNotFoundException("Il docente non esiste.");
    }
}