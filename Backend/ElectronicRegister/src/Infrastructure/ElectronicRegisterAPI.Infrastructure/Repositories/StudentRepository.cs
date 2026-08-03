using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

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

    public async Task<List<StudentDto>> GetAllAsync()
    {
        var students = await _context.Students.ToListAsync();
        return students.Select(MapToDto).ToList();
    }

    public async Task<StudentDto?> GetByIdAsync(Guid id)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);
        return student == null ? null : MapToDto(student);
    }

    public async Task<List<StudentDto>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var students = await _context.Students.Where(s => ids.Contains(s.Id)).ToListAsync();
        return students.Select(MapToDto).ToList();
    }

    public async Task<List<StudentDto>> GetByLastNameAsync(string lastName)
    {
        var students = await _context.Students.Where(s => s.LastName == lastName).ToListAsync();
        return students.Select(MapToDto).ToList();
    }

    public async Task AddAsync(StudentDto studentDto)
    {
        var student = MapToEntity(studentDto);
        _context.Students.Add(student);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(StudentDto studentDto)
    {
        var student = MapToEntity(studentDto);
        _context.Students.Update(student);
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

    private static StudentDto MapToDto(Student student)
    {
        return new StudentDto
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName
        };
    }

    private static Student MapToEntity(StudentDto studentDto)
    {
        return new Student
        {
            Id = studentDto.Id,
            FirstName = studentDto.FirstName,
            LastName = studentDto.LastName
        };
    }
}
