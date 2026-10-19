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
    private IQueryable<T> Rows<T>() where T : class => _scope?.Query<T>() ?? _context.Set<T>();
    private Task SaveAsync() => _scope is null ? _context.SaveChangesAsync() : _scope.SaveAsync();

    public StudentRepository(ElectronicRegisterContext context, InstitutionExamDataScope? scope = null)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<int> CountAsync(Guid? teacherId = null)
    {
        return await VisibleToTeacher(teacherId).CountAsync();
    }

    public async Task<List<Student>> GetAllAsync(Guid? teacherId = null)
    {
        var students = await VisibleToTeacher(teacherId).ToListAsync();
        return students.Select(MapToModel).ToList();
    }

    public async Task<Student?> GetByIdAsync(Guid id, Guid? teacherId = null)
    {
        var student = await VisibleToTeacher(teacherId).FirstOrDefaultAsync(s => s.Id == id);
        return student == null ? null : MapToModel(student);
    }

    public async Task<List<Student>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var students = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Student>().Where(s => ids.Contains(s.Id)).ToListAsync();
        return students.Select(MapToModel).ToList();
    }

    public async Task<List<Student>> GetByLastNameAsync(string lastName, Guid? teacherId = null)
    {
        var students = await VisibleToTeacher(teacherId).Where(s => s.LastName == lastName).ToListAsync();
        return students.Select(MapToModel).ToList();
    }

    public async Task AddAsync(Student student)
    {
        var studentEntity = new StudentEntity
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            ClassId = student.ClassId
        };
        _context.Students.Add(studentEntity);
        await SaveAsync();
    }

    public async Task UpdateAsync(Student student)
    {
        var entity = await MapToEntityAsync(student);
        if (entity == null) return;
        _context.Students.Update(entity);
        await SaveAsync();
    }

    public async Task DeleteAsync(Student student)
    {
        var entity = await MapToEntityAsync(student);
        if (entity == null) return;
        _context.Students.Remove(entity);
        await SaveAsync();
    }

    private IQueryable<StudentEntity> VisibleToTeacher(Guid? teacherId) =>
        Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Student>().AsNoTracking().Where(s => !teacherId.HasValue ||
            (s.Class != null && s.Class.ClassSubjects.Any(cs => cs.TeacherId == teacherId.Value)));

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

    private async Task<StudentEntity?> MapToEntityAsync(Student student)
    {
        var entity = await Rows<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Student>().FirstOrDefaultAsync(s => s.Id == student.Id);
        if (entity is null) return null;

        entity.Id = student.Id;
        entity.FirstName = student.FirstName;
        entity.LastName = student.LastName;
        entity.ClassId = student.ClassId;

        return entity;
    }
}
