using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal class TeacherRepository : ITeacherRepository
{
    private readonly ElectronicRegisterContext _context;

    public TeacherRepository(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task<int> CountAsync()
    {
        return await _context.Teachers.CountAsync();
    }

    public async Task<List<TeacherDto>> GetAllAsync()
    {
        var teachers = await _context.Teachers.ToListAsync();
        return teachers.Select(MapToDto).ToList();
    }

    public async Task<TeacherDto?> GetByIdAsync(Guid id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        return teacher == null ? null : MapToDto(teacher);
    }

    public async Task<List<TeacherDto>> GetByLastNameAsync(string lastName)
    {
        var teachers = await _context.Teachers.Where(t => t.LastName == lastName).ToListAsync();
        return teachers.Select(MapToDto).ToList();
    }

    public async Task AddAsync(TeacherDto teacherDto)
    {
        var teacher = MapToEntity(teacherDto);
        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(TeacherDto teacherDto)
    {
        var teacher = MapToEntity(teacherDto);
        _context.Teachers.Update(teacher);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        if (teacher != null)
        {
            _context.Teachers.Remove(teacher);
            await _context.SaveChangesAsync();
        }
    }

    private static TeacherDto MapToDto(Teacher teacher)
    {
        return new TeacherDto
        {
            Id = teacher.Id,
            FirstName = teacher.FirstName,
            LastName = teacher.LastName
        };
    }

    private static Teacher MapToEntity(TeacherDto teacherDto)
    {
        return new Teacher
        {
            Id = teacherDto.Id,
            FirstName = teacherDto.FirstName,
            LastName = teacherDto.LastName
        };
    }
}
