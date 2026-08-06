using Microsoft.EntityFrameworkCore;
using Student = ElectronicRegisterAPI.Domain.Models.Student;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using StudentEntity = ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Student;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class StudentRepository : IStudentRepository
{
    private readonly ElectronicRegisterContext _context;

    public StudentRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.Students.CountAsync();
    }

    public async Task<List<Student>> GetAllAsync()
    {
        var students = await _context.Students.ToListAsync();
        return students.Select(MapToModel).ToList();
    }

    public async Task<Student?> GetByIdAsync(Guid id)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);
        return student == null ? null : MapToModel(student);
    }

    public async Task<List<Student>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var students = await _context.Students.Where(s => ids.Contains(s.Id)).ToListAsync();
        return students.Select(MapToModel).ToList();
    }

    public async Task<List<Student>> GetByLastNameAsync(string lastName)
    {
        var students = await _context.Students.Where(s => s.LastName == lastName).ToListAsync();
        return students.Select(MapToModel).ToList();
    }

    public async Task AddAsync(Student student)
    {
        _context.Students.Add(MapToEntity(student));
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Student student)
    {
        _context.Students.Update(MapToEntity(student));
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student != null)
        {
            _context.Students.Remove(student);
            await _context.SaveChangesAsync();
        }
    }

    private static Student MapToModel(StudentEntity student)
    {
        return new Student
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName
        };
    }

    private static StudentEntity MapToEntity(Student student)
    {
        return new StudentEntity
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName
        };
    }
}