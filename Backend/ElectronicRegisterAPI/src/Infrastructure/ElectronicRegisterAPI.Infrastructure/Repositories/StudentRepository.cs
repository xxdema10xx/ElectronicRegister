using ElectronicRegisterAPI.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Student = ElectronicRegisterAPI.Domain.Models.Student;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using StudentEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Student;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class StudentRepository : IStudentRepository
{
    private readonly ElectronicRegisterContext _context;
    private readonly InstitutionExamDataScope? _scope;
    private CancellationToken Ct => _scope?.CancellationToken ?? CancellationToken.None;
    private IQueryable<T> Rows<T>()
        where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    public StudentRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<Student?> GetByIdAsync(Guid id)
    {
        var student = await Rows<StudentEntity>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, Ct);
        return student == null ? null : MapToModel(student);
    }

    public async Task<List<Student>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var students = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Student>().Where(s => ids.Contains(s.Id)).ToListAsync(Ct);
        return students.Select(MapToModel).ToList();
    }

    private static Student MapToModel(StudentEntity student)
    {
        return new Student
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            ClassId = student.ClassId
        };
    }

}
